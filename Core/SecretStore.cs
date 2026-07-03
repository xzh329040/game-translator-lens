using System.Security.Cryptography;
using System.Text;

namespace GameTranslatorLens.Core;

public static class SecretStore
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GameTranslatorLens.ApiKey.v1");

    public static string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return "";
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText.Trim());
        try
        {
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static string ProtectIfChanged(string plainText, string existingProtected)
    {
        string normalized = plainText.Trim();
        if (normalized.Length == 0)
        {
            return "";
        }

        if (TryUnprotect(existingProtected, out string current) &&
            string.Equals(current, normalized, StringComparison.Ordinal))
        {
            return existingProtected;
        }

        return Protect(normalized);
    }

    public static bool TryUnprotect(string protectedText, out string plainText)
    {
        plainText = "";
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return true;
        }

        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            plainText = protectedText.Trim();
            return true;
        }

        try
        {
            byte[] protectedBytes = Convert.FromBase64String(protectedText[Prefix.Length..]);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                plainText = Encoding.UTF8.GetString(plainBytes).Trim();
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
        catch
        {
            plainText = "";
            return false;
        }
    }
}
