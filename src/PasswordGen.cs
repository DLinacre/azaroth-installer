using System.Security.Cryptography;

namespace AzarothInstaller;

public static class PasswordGen
{
    static readonly char[] Alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*_-".ToCharArray();

    /// <summary>Generates a cryptographically secure random password.</summary>
    public static string Generate(int length = 24)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }
        return new string(chars);
    }
}
