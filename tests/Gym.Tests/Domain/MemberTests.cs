using Gym.Domain;

namespace Gym.Tests.Domain;

public sealed class MemberTests
{
    [Fact]
    public void Qr_code_is_random_well_formed_and_never_repeats()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => MemberQrCode.Generate()).ToList();

        Assert.All(codes, code =>
        {
            Assert.True(MemberQrCode.IsWellFormed(code), code);
            Assert.StartsWith(MemberQrCode.Prefix, code);
            Assert.Equal(MemberQrCode.TotalLength, code.Length);
        });
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TF1-")]
    [InlineData("12345678901")] // a national number is never a QR code
    [InlineData("TF1-0123456789ABCDEFGHJKMNP")] // one character short
    [InlineData("TF1-0123456789ABCDEFGHJKMNPQ1")] // one character too many
    [InlineData("TF1-0123456789ABCDEFGHJKMNPI")] // I is not in the alphabet
    [InlineData("XX1-0123456789ABCDEFGHJKMNPQ")]
    public void Malformed_qr_codes_are_rejected(string? code)
    {
        Assert.False(MemberQrCode.IsWellFormed(code));
    }

    [Fact]
    public void Scanned_qr_code_is_trimmed_and_upper_cased()
    {
        var code = MemberQrCode.Generate();

        Assert.Equal(code, MemberQrCode.Normalize("  " + code.ToLowerInvariant() + "\r\n"));
    }

    [Fact]
    public void Reissuing_the_qr_code_replaces_the_old_one()
    {
        var member = new Member { QrCode = MemberQrCode.Generate() };
        var old = member.QrCode;

        member.ReissueQrCode();

        Assert.NotEqual(old, member.QrCode);
        Assert.True(MemberQrCode.IsWellFormed(member.QrCode));
    }

    [Theory]
    [InlineData(412, "#00412")]
    [InlineData(1, "#00001")]
    [InlineData(123456, "#123456")]
    public void Member_number_display_format(int number, string expected)
    {
        Assert.Equal(expected, MemberNumber.Format(number));
    }

    [Theory]
    [InlineData("#00412", 412)]
    [InlineData("00412", 412)]
    [InlineData(" 412 ", 412)]
    [InlineData("٤١٢", 412)]
    [InlineData("#٠٠٤١٢", 412)]
    public void Member_number_parses_typed_text(string text, int expected)
    {
        Assert.True(MemberNumber.TryParse(text, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("0")]
    [InlineData("12a")]
    [InlineData("-5")]
    [InlineData("1234567890")] // too long to be a member number
    public void Member_number_rejects_other_text(string? text)
    {
        Assert.False(MemberNumber.TryParse(text, out _));
    }

    [Fact]
    public void Full_name_joins_first_father_and_last_name()
    {
        var member = new Member { FirstName = " Ahmad ", FatherName = "Khaled", LastName = "Haddad", MotherName = "Mona" };

        Assert.Equal("Ahmad Khaled Haddad", member.FullName);
        member.FatherName = null;
        Assert.Equal("Ahmad Haddad", member.FullName);
    }

    [Fact]
    public void Search_name_is_normalized_and_includes_the_mother_name()
    {
        var member = new Member { FirstName = "أحمد", FatherName = "خالد", LastName = "الحدّاد", MotherName = "فاطمة" };

        member.RefreshSearchName();

        Assert.Equal("احمد خالد الحداد فاطمه", member.SearchName);
    }

    [Fact]
    public void Client_can_be_linked_to_a_private_trainer()
    {
        var trainer = new Member { Id = Guid.NewGuid(), Type = MemberType.PrivateTrainer };
        var client = new Member { Id = Guid.NewGuid() };

        client.LinkToPrivateTrainer(trainer);
        Assert.Equal(trainer.Id, client.PrivateTrainerId);

        client.UnlinkPrivateTrainer();
        Assert.Null(client.PrivateTrainerId);
    }

    [Fact]
    public void Client_cannot_be_linked_to_a_regular_member()
    {
        var regular = new Member { Id = Guid.NewGuid(), Type = MemberType.Regular, MemberNo = 7 };
        var client = new Member { Id = Guid.NewGuid() };

        var error = Assert.Throws<DomainException>(() => client.LinkToPrivateTrainer(regular));

        Assert.Equal("not_a_private_trainer", error.Code);
        Assert.Contains("#00007", error.Message);
        Assert.Null(client.PrivateTrainerId);
    }

    [Fact]
    public void Private_trainer_cannot_be_his_own_client()
    {
        var trainer = new Member { Id = Guid.NewGuid(), Type = MemberType.PrivateTrainer };

        Assert.Equal("cannot_link_self", Assert.Throws<DomainException>(() => trainer.LinkToPrivateTrainer(trainer)).Code);
    }

    [Fact]
    public void Private_trainer_with_clients_cannot_become_regular()
    {
        var trainer = new Member { Type = MemberType.PrivateTrainer };

        Assert.Equal("trainer_has_clients", Assert.Throws<ConflictException>(() => trainer.ChangeType(MemberType.Regular, 2)).Code);

        trainer.ChangeType(MemberType.Regular, 0);
        Assert.Equal(MemberType.Regular, trainer.Type);
    }
}
