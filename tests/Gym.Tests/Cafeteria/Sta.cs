using System.Runtime.ExceptionServices;

namespace Gym.Tests.Cafeteria;

/// <summary>Runs WPF code on its own STA thread (WPF elements need one; xUnit threads are MTA).</summary>
internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
#pragma warning disable CA1031 // Rethrown on the test thread below.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
