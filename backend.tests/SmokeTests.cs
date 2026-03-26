using BankReporting.Api;
using System.Text.Json;
using Xunit;

public class SmokeTests
{
    [Theory]
    [InlineData("weak", false)]
    [InlineData("Strong#123456", true)]
    public void PasswordPolicy_Works(string password, bool expected)
    {
        Assert.Equal(expected, SecurityHelpers.IsStrongPassword(password));
    }

    [Fact]
    public void PasswordHash_Roundtrip_Works()
    {
        var hash = SecurityHelpers.HashPassword("Strong#123456");
        Assert.True(SecurityHelpers.VerifyPassword("Strong#123456", hash));
        Assert.False(SecurityHelpers.VerifyPassword("wrong", hash));
    }

    [Fact]
    public void CsvExport_Works()
    {
        using var doc = JsonDocument.Parse("{\"reportMonth\":\"2026-01\",\"amount\":123}");
        var csv = SecurityHelpers.BuildCsv(doc);
        Assert.Contains("reportMonth", csv);
        Assert.Contains("2026-01", csv);
    }

    [Fact]
    public void XlsxExport_Generates_OpenXmlZip()
    {
        using var doc = JsonDocument.Parse("{\"reportMonth\":\"2026-01\",\"amount\":123}");
        var bytes = SecurityHelpers.BuildXlsx(doc);
        Assert.True(bytes.Length > 100);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void Totp_InvalidCode_Fails()
    {
        var secret = SecurityHelpers.NewTotpSecret();
        Assert.False(SecurityHelpers.VerifyTotp(secret, "000000"));
    }
}
