using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Logging;

namespace RivianMate.Api.Security;

/// <summary>
/// Encrypts Data Protection keys using a master key from an environment variable.
/// This adds an additional layer of protection - even if the database is compromised,
/// the attacker cannot decrypt the Data Protection keys without the master key.
/// </summary>
public class EnvironmentKeyXmlEncryptor : IXmlEncryptor
{
    private readonly byte[] _masterKey;

    public EnvironmentKeyXmlEncryptor(string masterKeyEnvVar)
    {
        var masterKey = Environment.GetEnvironmentVariable(masterKeyEnvVar);
        if (string.IsNullOrEmpty(masterKey))
        {
            throw new InvalidOperationException(
                $"Environment variable '{masterKeyEnvVar}' is required for Data Protection key encryption.");
        }

        // Derive a 256-bit key from the master key using PBKDF2
        _masterKey = DeriveKey(masterKey);
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString());

        // Generate a random IV for each encryption
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        // Combine IV + ciphertext and base64 encode
        var combined = new byte[iv.Length + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, iv.Length, ciphertext.Length);

        var encryptedElement = new XElement("encryptedKey",
            new XComment("This key is encrypted with a master key from the environment"),
            new XElement("value", Convert.ToBase64String(combined)));

        return new EncryptedXmlInfo(encryptedElement, typeof(EnvironmentKeyXmlDecryptor));
    }

    private static byte[] DeriveKey(string password)
    {
        // Use a fixed salt - this is acceptable because the password itself should be unique per deployment
        // The salt prevents rainbow table attacks, not per-encryption uniqueness (that's the IV's job)
        var salt = "RivianMate.DataProtection.v1"u8.ToArray();
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
    }
}

/// <summary>
/// Decrypts Data Protection keys that were encrypted with EnvironmentKeyXmlEncryptor.
/// </summary>
public class EnvironmentKeyXmlDecryptor : IXmlDecryptor
{
    private readonly byte[]? _masterKey;
    private readonly ILogger<EnvironmentKeyXmlDecryptor>? _logger;

    public EnvironmentKeyXmlDecryptor(IServiceProvider services)
    {
        _logger = services.GetService<ILogger<EnvironmentKeyXmlDecryptor>>();

        var masterKey = Environment.GetEnvironmentVariable("RIVIANMATE_DP_KEY");

        if (string.IsNullOrEmpty(masterKey))
        {
            _logger?.LogCritical(
                "RIVIANMATE_DP_KEY environment variable is not set but encrypted Data Protection keys exist. " +
                "The application cannot decrypt Rivian API tokens. " +
                "Either set the RIVIANMATE_DP_KEY to the original value, or users will need to re-link their Rivian accounts.");
            _masterKey = null;
        }
        else
        {
            _masterKey = DeriveKey(masterKey);
        }
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        if (_masterKey == null)
        {
            throw new InvalidOperationException(
                "Cannot decrypt Data Protection key: RIVIANMATE_DP_KEY environment variable is not set. " +
                "This key was encrypted with a master key that is no longer available. " +
                "Set RIVIANMATE_DP_KEY to the original value to restore access.");
        }

        var valueElement = encryptedElement.Element("value");
        if (valueElement == null)
        {
            throw new InvalidOperationException("Encrypted element does not contain a 'value' element.");
        }

        var combined = Convert.FromBase64String(valueElement.Value);

        // Extract IV (first 16 bytes) and ciphertext
        var iv = new byte[16];
        var ciphertext = new byte[combined.Length - 16];
        Buffer.BlockCopy(combined, 0, iv, 0, 16);
        Buffer.BlockCopy(combined, 16, ciphertext, 0, ciphertext.Length);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }

    private static byte[] DeriveKey(string password)
    {
        var salt = "RivianMate.DataProtection.v1"u8.ToArray();
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
    }
}
