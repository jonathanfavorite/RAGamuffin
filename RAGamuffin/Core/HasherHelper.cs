using System.Security.Cryptography;
using System.Text;

namespace RAGamuffin.Core;
internal static class HasherHelper
{
    internal static string ComputeSha256Hash(string text)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}
