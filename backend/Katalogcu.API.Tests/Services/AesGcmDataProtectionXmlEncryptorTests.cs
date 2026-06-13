using System.Xml.Linq;
using Katalogcu.API.Services;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class AesGcmDataProtectionXmlEncryptorTests
{
    [Fact]
    public void Encrypt_DecryptsBackToOriginalXml()
    {
        var key = Convert.ToBase64String("0123456789abcdef0123456789abcdef"u8.ToArray());
        var encryptor = new AesGcmDataProtectionXmlEncryptor(key);
        var plaintext = new XElement("key", new XAttribute("id", "test"), new XElement("secret", "value"));

        var encrypted = encryptor.Encrypt(plaintext);
        var encryptedText = encrypted.EncryptedElement.ToString(SaveOptions.DisableFormatting);
        var decrypted = encryptor.Decrypt(encrypted.EncryptedElement);

        Assert.DoesNotContain("value", encryptedText);
        Assert.Equal("AES-256-GCM", encrypted.EncryptedElement.Attribute("algorithm")?.Value);
        Assert.True(XNode.DeepEquals(plaintext, decrypted));
    }

    [Fact]
    public void Constructor_RejectsInvalidKeyLength()
    {
        var invalidKey = Convert.ToBase64String("too-short"u8.ToArray());

        Assert.Throws<InvalidOperationException>(() => new AesGcmDataProtectionXmlEncryptor(invalidKey));
    }
}
