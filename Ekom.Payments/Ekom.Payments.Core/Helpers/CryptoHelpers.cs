using System;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Payments.Helpers;

/// <summary>
/// Perform crypto calculations
/// </summary>
public static class CryptoHelpers
{
    /// <summary>
    /// Valitor - Calculates MD5 hash from the provided string input.
    /// </summary>
    /// <param name="input">String to hash</param>
    public static string GetMD5StringSum(string input)
    {
        using MD5 md5Hasher = MD5.Create();
        byte[] data = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(input));

        StringBuilder sBuilder = new StringBuilder();

        for (int i = 0; i < data.Length; i++)
        {
            sBuilder.Append(data[i].ToString("x2"));
        }
        return sBuilder.ToString();
    }

    /// <summary>
    /// Computes SHA256 sum from string, returns as hexadecimal string
    /// ToDo: Use BitConverter? Returns as uppercase hex string instead of lowercase though.
    /// </summary>
    /// <param name="input"></param>
    /// <returns>SHA256 sum as hex string</returns>
    public static string GetSHA256HexStringSum(string input)
    {
        using SHA256 sha = SHA256.Create();
        byte[] data = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

        StringBuilder sBuilder = new StringBuilder();

        for (int i = 0; i < data.Length; i++)
        {
            sBuilder.Append(data[i].ToString("x2"));
        }
        return sBuilder.ToString();
    }

    /// <summary>
    /// Computes SHA256 sum from string, returns as Base64 encoded string
    /// </summary>
    /// <param name="input"></param>
    /// <returns>Base64 encoded SHA256 sum</returns>
    public static string GetSHA256StringSum(string input)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    /// <summary>
    /// Borgun - CreateCheckHash
    /// </summary>
    public static string GetHMACSHA256(string secretcode, string message)
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(secretcode);

        using var hasher = new HMACSHA256(secretBytes);
        byte[] result = hasher.ComputeHash(Encoding.UTF8.GetBytes(message));

        string checkhash = BitConverter.ToString(result).Replace("-", "");

        return checkhash;
    }
}
