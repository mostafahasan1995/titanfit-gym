using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.Reception.Logic;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Reception.Views;

/// <summary>The reception dialogs: renew, private training, member QR, phone, photo, pick a member.</summary>
internal static class ReceptionDialogs
{
    /// <summary>Months offered for the private training service.</summary>
    public static readonly int[] PrivateServiceMonths = [1, 3, 6, 12];

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>The member's data as an update request (to change one field and keep the others).</summary>
    public static UpdateMemberRequest ToUpdate(MemberDto member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return new UpdateMemberRequest(
            member.FirstName, member.LastName, member.FatherName, member.MotherName, member.BirthDate, member.BirthPlace,
            member.NationalNo, member.Phone, member.Notes, member.Type);
    }

    /// <summary>Renew a membership: choose the plan, see the new dates and the price, confirm. The new subscription, or null.</summary>
    public static async Task<SubscriptionDto?> RenewAsync(PageContext context, MemberDto member, ImageSource? photo)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(member);
        var language = context.Language;
        var plans = await context.Api.GetPlansAsync().ConfigureAwait(true);
        var choices = plans.Select(p => new PeriodChoice(p.Months, p.Price, p.Id)).ToList();
        var person = Parts.Person(
            member.FullName,
            Parts.IdLine(member.MemberNoText, member.Phone, language),
            photo,
            CurrentLine(member.Membership, language));
        var view = new PeriodChoiceView(person, ReceptionText.Duration, choices, member.Membership?.EndDate, Today, preselectMonths: 1);
        SubscriptionDto? renewed = null;
        await context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = ReceptionText.RenewTitle,
            Content = view,
            PrimaryText = ReceptionText.RenewConfirm,
            Tone = DialogTone.Gold,
            Width = 640,
            OnPrimary = () => TryAsync(context, async () =>
            {
                if (!view.Validate() || view.Selected?.PlanId is not { } planId)
                {
                    return false;
                }

                renewed = await context.Api.RenewMemberAsync(member.Id, planId).ConfigureAwait(true);
                return true;
            }),
        }).ConfigureAwait(true);

        if (renewed is not null)
        {
            context.Ui.Toast(BidiText.FixNumbers(ReceptionText.RenewedUntil.Format(context.Language, DateText.ShortDate(renewed.EndDate, context.Language)), context.Language));
        }

        return renewed;
    }

    /// <summary>Buy or renew the private training service (months × the monthly fee). The new period, or null.</summary>
    public static async Task<SubscriptionDto?> PayPrivateServiceAsync(
        PageContext context, Guid trainerId, string name, string memberNoText, string? phone, SubscriptionStatusDto? service, ImageSource? photo)
    {
        ArgumentNullException.ThrowIfNull(context);
        var language = context.Language;
        var settings = await context.Api.GetSettingsAsync().ConfigureAwait(true);
        var fee = settings.PrivateTrainingMonthlyFee;
        var choices = PrivateServiceMonths.Select(m => new PeriodChoice(m, PrivateTrainingRules.CalculatePrice(m, fee), null)).ToList();
        var person = Parts.Person(name, Parts.IdLine(memberNoText, phone, language), photo, ServiceLine(service, language));
        var view = new PeriodChoiceView(person, ReceptionText.PrivateServiceDuration, choices, service?.EndDate, Today, preselectMonths: 1);
        SubscriptionDto? paid = null;
        await context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = ReceptionText.PayPrivateTitle,
            Message = ReceptionText.PayPrivateText,
            Content = view,
            PrimaryText = ReceptionText.PayConfirm,
            Tone = DialogTone.Gold,
            Width = 640,
            OnPrimary = () => TryAsync(context, async () =>
            {
                if (!view.Validate() || view.Selected is not { } choice)
                {
                    return false;
                }

                paid = await context.Api.AddPrivateServiceAsync(trainerId, choice.Months).ConfigureAwait(true);
                return true;
            }),
        }).ConfigureAwait(true);

        if (paid is not null)
        {
            context.Ui.Toast(BidiText.FixNumbers(ReceptionText.PrivatePaidUntil.Format(language, DateText.ShortDate(paid.EndDate, language)), language));
        }

        return paid;
    }

    /// <summary>Shows the member QR again (copy, save, print) and can issue a new one. True when a new QR was issued.</summary>
    public static async Task<bool> ShowQrAsync(PageContext context, MemberDto member)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(member);
        var qr = new MemberQrView { Ui = context.Ui, QrSize = 236 };
        qr.Show(member.QrCode, member.MemberNoText, member.FullName);
        var reissued = false;
        var reissue = new Button
        {
            Content = ReceptionText.ReissueQr.For(context.Language),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 16, 0, 0),
        };
        reissue.SetResourceReference(FrameworkElement.StyleProperty, "Button.Ghost");
        ThemeAssist.SetIcon(reissue, UiIcons.Renew);
        reissue.Click += async (_, _) =>
        {
            var sure = await context.Ui.ConfirmAsync(ReceptionText.ReissueQrTitle, ReceptionText.ReissueQrText, ReceptionText.ReissueQr, danger: true).ConfigureAwait(true);
            if (!sure)
            {
                return;
            }

            try
            {
                var code = await context.Api.ReissueQrCodeAsync(member.Id).ConfigureAwait(true);
                qr.Show(code.QrCode, member.MemberNoText, member.FullName);
                reissued = true;
                context.Ui.Toast(ReceptionText.QrReissued);
            }
            catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException)
            {
                ReceptionErrors.Show(context, ex);
            }
        };
        var content = new StackPanel();
        content.Children.Add(qr);
        content.Children.Add(reissue);
        await context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = ReceptionText.MemberQrTitle,
            Content = content,
            PrimaryText = CommonText.Close,
            SecondaryText = null,
            Width = 640,
        }).ConfigureAwait(true);
        return reissued;
    }

    /// <summary>Change the phone number. The updated member, or null.</summary>
    public static async Task<MemberDto?> EditPhoneAsync(PageContext context, MemberDto member)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(member);
        var input = new PhoneInput(member.Phone);
        MemberDto? updated = null;
        await context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = ReceptionText.EditPhoneTitle,
            Content = input,
            PrimaryText = CommonText.Save,
            Width = 520,
            OnPrimary = () => TryAsync(context, async () =>
            {
                updated = await context.Api.UpdateMemberAsync(member.Id, ToUpdate(member) with { Phone = input.Phone }).ConfigureAwait(true);
                return true;
            }),
        }).ConfigureAwait(true);

        if (updated is not null)
        {
            context.Ui.Toast(ReceptionText.PhoneSaved);
        }

        return updated;
    }

    /// <summary>Take a new photo with the webcam and upload it. True when saved.</summary>
    public static async Task<bool> ChangePhotoAsync(PageContext context, MemberDto member)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(member);
        var view = new PhotoCaptureView(context.Station.CameraName, context.IsSample);
        var saved = await context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = ReceptionText.ChangePhotoTitle,
            Content = view,
            PrimaryText = ReceptionText.SavePhoto,
            Width = 480,
            OnPrimary = () => TryAsync(context, async () =>
            {
                if (!view.Validate() || view.Photo is not { } photo)
                {
                    return false;
                }

                await context.Api.UploadMemberPhotoAsync(member.Id, photo, "image/jpeg").ConfigureAwait(true);
                return true;
            }),
        }).ConfigureAwait(true);

        if (saved)
        {
            context.Ui.Toast(ReceptionText.PhotoSaved);
        }

        return saved;
    }

    /// <summary>Search and pick a member, then run <paramref name="onPicked"/> (the dialog stays open when it fails).</summary>
    public static async Task<MemberSummary?> PickMemberAsync(
        PageContext context, LocalText title, LocalText primary, Func<MemberSummary, bool> canPick, Func<MemberSummary, string?> note, Func<MemberSummary, Task> onPicked)
    {
        ArgumentNullException.ThrowIfNull(context);
        var view = new MemberPickerView(context.Api, canPick, note);
        await view.LoadAsync().ConfigureAwait(true);
        MemberSummary? picked = null;
        await context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = title,
            Content = view,
            PrimaryText = primary,
            Width = 620,
            OnPrimary = () => TryAsync(context, async () =>
            {
                if (!view.Validate() || view.Selected is not { } member)
                {
                    return false;
                }

                await onPicked(member).ConfigureAwait(true);
                picked = member;
                return true;
            }),
        }).ConfigureAwait(true);
        return picked;
    }

    /// <summary>"Valid until 26 Dec 2026" / "Expired on 20 Nov 2026" / "No subscription yet".</summary>
    public static string CurrentLine(SubscriptionStatusDto? membership, AppLanguage language) => membership switch
    {
        null => ReceptionText.StatusNoSubscription.For(language),
        { Status: SubscriptionStatus.Expired } expired => ReceptionText.ExpiredOn.Format(language, DateText.ShortDate(expired.EndDate, language)),
        { } valid => ReceptionText.ValidUntil.Format(language, DateText.ShortDate(valid.EndDate, language)),
    };

    /// <summary>"Private training paid until 15 Dec 2026" / "Private training not paid" / "... ended on ...".</summary>
    public static string ServiceLine(SubscriptionStatusDto? service, AppLanguage language) => service switch
    {
        null => ReceptionText.ServiceNotPaid.For(language),
        { Status: SubscriptionStatus.Expired } expired => ReceptionText.ServiceEndedOn.Format(language, DateText.ShortDate(expired.EndDate, language)),
        { } valid => ReceptionText.ServicePaidUntil.Format(language, DateText.ShortDate(valid.EndDate, language)),
    };

    /// <summary>Runs a dialog action; a server refusal shows as a red toast and keeps the dialog open.</summary>
    private static async Task<bool> TryAsync(PageContext context, Func<Task<bool>> action)
    {
        try
        {
            return await action().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(context, ex);
            return false;
        }
    }
}
