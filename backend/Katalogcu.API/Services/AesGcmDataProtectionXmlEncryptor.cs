using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Katalogcu.API.Services;

public sealed class AesGcmDataProtectionXmlEncryptor : IXmlEncryptor, IXmlDecryptor
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const string EnvKeyName = "DataProtection__KeyEncryptionKey";
    private static byte[]? s_key;

    public AesGcmDataProtectionXmlEncryptor()
    {
    }

    public AesGcmDataProtectionXmlEncryptor(string base64Key)
    {
        s_key = DecodeKey(base64Key);
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var encryptedElement = new XElement(
            "encryptedData",
            new XAttribute("algorithm", "AES-256-GCM"),
            new XElement("nonce", Convert.ToBase64String(nonce)),
            new XElement("ciphertext", Convert.ToBase64String(ciphertext)),
            new XElement("tag", Convert.ToBase64String(tag)));

        return new EncryptedXmlInfo(encryptedElement, typeof(AesGcmDataProtectionXmlEncryptor));
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        var key = GetKey();
        var nonce = Convert.FromBase64String(GetRequiredElementValue(encryptedElement, "nonce"));
        var ciphertext = Convert.FromBase64String(GetRequiredElementValue(encryptedElement, "ciphertext"));
        var tag = Convert.FromBase64String(GetRequiredElementValue(encryptedElement, "tag"));
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }

    public static bool IsConfigured(string? base64Key) =>
        !string.IsNullOrWhiteSpace(base64Key);

    private static byte[] GetKey()
    {
        if (s_key is not null)
        {
            return s_key;
        }

        s_key = DecodeKey(Environment.GetEnvironmentVariable(EnvKeyName));
        return s_key;
    }

    private static byte[] DecodeKey(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new InvalidOperationException("DataProtection key encryption key is not configured.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("DataProtection key encryption key must be base64 encoded.", ex);
        }

        if (key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException("DataProtection key encryption key must decode to 16, 24, or 32 bytes.");
        }

        return key;
    }

    private static string GetRequiredElementValue(XElement parent, string name)
    {
        return parent.Element(name)?.Value
            ?? throw new InvalidOperationException($"Encrypted DataProtection XML is missing '{name}'.");
    }
}
