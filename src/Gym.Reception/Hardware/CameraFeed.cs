using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gym.Reception.Logic;
using OpenCvSharp;

namespace Gym.Reception.Hardware;

public enum CameraState
{
    Off,
    Starting,
    Running,

    /// <summary>The PC has no webcam.</summary>
    NoCamera,

    /// <summary>The camera did not open or stopped sending pictures (in use by another program, unplugged, driver).</summary>
    Failed,
}

/// <summary>
/// The webcam, read with OpenCvSharp (DirectShow). A background thread reads the frames, shows them in
/// <see cref="Preview"/> and, while a page asks for it, looks for a member QR code in them (ZXing.Net).
/// One camera is open at a time; the check-in and new-member pages share <see cref="Shared"/> (a page
/// <see cref="Acquire"/>s it when shown and <see cref="Release"/>s it when it goes away). Events come on the UI thread.
/// </summary>
public sealed class CameraFeed
{
    private const int RequestedWidth = 1280;
    private const int RequestedHeight = 720;
    private const int DecodeEveryMs = 120;

    private readonly object frameLock = new();
    private readonly Dictionary<object, bool> owners = [];
    private Dispatcher? dispatcher;
    private Thread? thread;
    private CancellationTokenSource? stop;
    private byte[] frameBuffer = [];
    private int frameWidth;
    private int frameHeight;
    private int frameStride;
    private int previewPending;
    private WriteableBitmap? preview;
    private volatile bool decodeQr;

    private CameraFeed()
    {
    }

    public static CameraFeed Shared { get; } = new();

    /// <summary>A new preview bitmap (first frame or another size). Read <see cref="Preview"/>.</summary>
    public event Action? PreviewChanged;

    public event Action<CameraState>? StateChanged;

    /// <summary>A QR code was seen (every frame it is in: debounce it).</summary>
    public event Action<string>? QrCodeFound;

    public CameraState State { get; private set; } = CameraState.Off;

    /// <summary>The error of the last failed start, for the screen.</summary>
    public string? Error { get; private set; }

    /// <summary>The camera in use (its label from <see cref="CameraDevices.List"/>).</summary>
    public string? CameraLabel { get; private set; }

    /// <summary>The live picture (updated in place for every frame).</summary>
    public ImageSource? Preview => preview;

    /// <summary>
    /// Starts the camera for <paramref name="owner"/> (a page), or keeps it running. <paramref name="decodeQr"/>: look for
    /// QR codes while this owner has it. A different <paramref name="label"/> switches to that camera.
    /// </summary>
    public void Acquire(object owner, string? label, bool decodeQr)
    {
        ArgumentNullException.ThrowIfNull(owner);
        dispatcher ??= Dispatcher.CurrentDispatcher;
        owners[owner] = decodeQr;
        this.decodeQr = owners.Values.Any(v => v);
        var cameras = CameraDevices.List();
        if (cameras.Count == 0)
        {
            StopThread();
            CameraLabel = null;
            SetState(CameraState.NoCamera, null);
            return;
        }

        var index = CameraDevices.IndexOf(cameras, label);
        var wanted = cameras[index];
        if (thread is not null && State is CameraState.Starting or CameraState.Running && wanted == CameraLabel)
        {
            return;
        }

        StopThread();
        CameraLabel = wanted;
        StartThread(index);
    }

    /// <summary>The owner does not need the camera any more; it stops when nobody else does (after the page switch).</summary>
    public void Release(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owners.Remove(owner))
        {
            return;
        }

        decodeQr = owners.Values.Any(v => v);
        _ = (dispatcher ?? Dispatcher.CurrentDispatcher).InvokeAsync(
            () =>
            {
                if (owners.Count == 0)
                {
                    StopThread();
                    SetState(CameraState.Off, null);
                }
            },
            DispatcherPriority.Background);
    }

    /// <summary>The latest frame cut to a centred square of <paramref name="size"/> pixels, as JPEG; null when there is no frame yet.</summary>
    public byte[]? CaptureSquareJpeg(int size = 480)
    {
        byte[] pixels;
        int width, height, stride;
        lock (frameLock)
        {
            if (frameWidth == 0)
            {
                return null;
            }

            pixels = (byte[])frameBuffer.Clone();
            (width, height, stride) = (frameWidth, frameHeight, frameStride);
        }

        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
        var square = PhotoCrop.CenterSquare(width, height);
        var cropped = new CroppedBitmap(source, new Int32Rect(square.X, square.Y, square.Side, square.Side));
        var scale = (double)size / square.Side;
        var scaled = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(scaled));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void StartThread(int index)
    {
        SetState(CameraState.Starting, null);
        stop = new CancellationTokenSource();
        var token = stop.Token;
        thread = new Thread(() => Run(index, token)) { IsBackground = true, Name = "TITANFIT camera" };
        thread.Start();
    }

    private void StopThread()
    {
        if (thread is null)
        {
            return;
        }

        stop?.Cancel();
        // The camera must be closed before it can be opened again; a frame takes at most a few hundred ms.
        thread.Join(TimeSpan.FromSeconds(2));
        stop?.Dispose();
        stop = null;
        thread = null;
        lock (frameLock)
        {
            frameWidth = 0;
        }
    }

    private void Run(int index, CancellationToken token)
    {
        try
        {
            using var capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
            if (!capture.IsOpened())
            {
                Post(() => SetState(CameraState.Failed, null));
                return;
            }

            capture.Set(VideoCaptureProperties.FrameWidth, RequestedWidth);
            capture.Set(VideoCaptureProperties.FrameHeight, RequestedHeight);
            using var frame = new Mat();
            using var gray = new Mat();
            var decoder = new QrDecoder();
            byte[] grayBuffer = [];
            long lastDecode = 0;
            var misses = 0;
            var running = false;
            while (!token.IsCancellationRequested)
            {
                if (!capture.Read(frame) || frame.Empty() || frame.Channels() != 3)
                {
                    if (++misses > 60)
                    {
                        Post(() => SetState(CameraState.Failed, null));
                        return;
                    }

                    Thread.Sleep(50);
                    continue;
                }

                misses = 0;
                if (!running)
                {
                    running = true;
                    Post(() => SetState(CameraState.Running, null));
                }

                CopyFrame(frame);
                SchedulePreview();

                if (decodeQr && Environment.TickCount64 - lastDecode >= DecodeEveryMs)
                {
                    lastDecode = Environment.TickCount64;
                    Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                    var length = gray.Width * gray.Height;
                    if (grayBuffer.Length != length)
                    {
                        grayBuffer = new byte[length];
                    }

                    if (gray.IsContinuous())
                    {
                        Marshal.Copy(gray.Data, grayBuffer, 0, length);
                        if (decoder.DecodeGray(grayBuffer, gray.Width, gray.Height) is { Length: > 0 } text)
                        {
                            Post(() => QrCodeFound?.Invoke(text));
                        }
                    }
                }
            }
        }
#pragma warning disable CA1031 // A camera or native library problem must not take the app down: report it on screen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Post(() => SetState(CameraState.Failed, message));
        }
    }

    private void CopyFrame(Mat frame)
    {
        var stride = (int)frame.Step();
        var length = stride * frame.Height;
        lock (frameLock)
        {
            if (frameBuffer.Length != length)
            {
                frameBuffer = new byte[length];
            }

            Marshal.Copy(frame.Data, frameBuffer, 0, length);
            frameWidth = frame.Width;
            frameHeight = frame.Height;
            frameStride = stride;
        }
    }

    private void SchedulePreview()
    {
        if (Interlocked.Exchange(ref previewPending, 1) == 1)
        {
            return;
        }

        Post(() =>
        {
            Interlocked.Exchange(ref previewPending, 0);
            UpdatePreview();
        });
    }

    private void UpdatePreview()
    {
        var changed = false;
        lock (frameLock)
        {
            if (frameWidth == 0)
            {
                return;
            }

            if (preview is null || preview.PixelWidth != frameWidth || preview.PixelHeight != frameHeight)
            {
                preview = new WriteableBitmap(frameWidth, frameHeight, 96, 96, PixelFormats.Bgr24, null);
                changed = true;
            }

            preview.WritePixels(new Int32Rect(0, 0, frameWidth, frameHeight), frameBuffer, frameStride, 0);
        }

        if (changed)
        {
            PreviewChanged?.Invoke();
        }
    }

    private void Post(Action action) => _ = dispatcher?.InvokeAsync(action, DispatcherPriority.Render);

    private void SetState(CameraState state, string? error)
    {
        Error = error;
        if (State != state || error is not null)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}

/// <summary>The square in the middle of a picture: the member photo is cut to it.</summary>
public static class PhotoCrop
{
    public static (int X, int Y, int Side) CenterSquare(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var side = Math.Min(width, height);
        return ((width - side) / 2, (height - side) / 2, side);
    }
}
