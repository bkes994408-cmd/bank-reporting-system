using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BankReporting.Api.Services;

public interface IJweEncryptionService
{
    string EncryptToCompactJwe(object payload, string publicKeyPem, string? keyId = null);
}

public class JweEncryptionService : IJweEncryptionService
{
    public string EncryptToCompactJwe(object payload, string publicKeyPem, string? keyId = null)
    {
        var header = new Dictionary<string, string>
        {
            ["alg"] = "RSA-OAEP-256",
            ["enc"] = "A256GCM",
            ["typ"] = "JWE"
        };

        if (!string.IsNullOrWhiteSpace(keyId))
        {
            header["kid"] = keyId.Trim();
        }

        var headerJson = JsonSerializer.Serialize(header);
        var protectedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        var cek = RandomNumberGenerator.GetBytes(32); // A256GCM
        var iv = RandomNumberGenerator.GetBytes(12);  // 96-bit nonce
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(cek, tag.Length))
        {
            aes.Encrypt(iv, plaintext, ciphertext, tag, Encoding.ASCII.GetBytes(protectedHeader));
        }

        byte[] encryptedKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(publicKeyPem);
            encryptedKey = rsa.Encrypt(cek, RSAEncryptionPadding.OaepSHA256);
        }

        return string.Join('.',
            protectedHeader,
            Base64UrlEncode(encryptedKey),
            Base64UrlEncode(iv),
            Base64UrlEncode(ciphertext),
            Base64UrlEncode(tag));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
