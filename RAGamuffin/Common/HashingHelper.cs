using System.Security.Cryptography;
using System.Text;

namespace RAGamuffin.Common;

/// <summary>
/// Utility class for cryptographic hashing operations.
/// Provides methods for generating SHA-256 hashes of text content.
/// </summary>
public static class HashingHelper
{
    /// <summary>
    /// Computes a SHA-256 hash of the provided text string.
    /// </summary>
    /// <param name="text">The text to hash</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null</exception>
    /// <exception cref="CryptographicException">Thrown when the cryptographic operation fails</exception>
    public static string ComputeSha256Hash(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        }

        try
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = sha256.ComputeHash(textBytes);
            return Convert.ToHexString(hashBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Failed to compute SHA-256 hash. The cryptographic service provider may not be available.", ex);
        }
    }

    /// <summary>
    /// Computes a SHA-256 hash of the provided text string and returns it as a byte array.
    /// </summary>
    /// <param name="text">The text to hash</param>
    /// <returns>A byte array containing the SHA-256 hash</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null</exception>
    /// <exception cref="CryptographicException">Thrown when the cryptographic operation fails</exception>
    public static byte[] ComputeSha256HashBytes(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        }

        try
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.UTF8.GetBytes(text);
            return sha256.ComputeHash(textBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Failed to compute SHA-256 hash. The cryptographic service provider may not be available.", ex);
        }
    }

    /// <summary>
    /// Verifies if a text string matches a given SHA-256 hash.
    /// </summary>
    /// <param name="text">The text to verify</param>
    /// <param name="expectedHash">The expected SHA-256 hash in hexadecimal format</param>
    /// <returns>True if the text matches the hash, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when text or expectedHash is null</exception>
    /// <exception cref="ArgumentException">Thrown when expectedHash is not a valid hexadecimal string</exception>
    /// <exception cref="CryptographicException">Thrown when the cryptographic operation fails</exception>
    public static bool VerifySha256Hash(string text, string expectedHash)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        }

        if (expectedHash is null)
        {
            throw new ArgumentNullException(nameof(expectedHash), "Expected hash cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64)
        {
            throw new ArgumentException("Expected hash must be a valid 64-character hexadecimal string.", nameof(expectedHash));
        }

        try
        {
            var computedHash = ComputeSha256Hash(text);
            return string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (CryptographicException)
        {
            // Re-throw cryptographic exceptions
            throw;
        }
    }
}
