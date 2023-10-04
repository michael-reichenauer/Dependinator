using System.Text.Json;

namespace DependinatorLib.Utils;


public static class StringExtensions
{
    // Method that limits the length of text to a defined length and can fill the rest with spaces
    public static string Max(this string source, int maxLength, bool isFill = false)
    {
        var text = source;
        if (isFill && text.Length < maxLength)
        {
            text += new string(' ', maxLength - text.Length);
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength);
    }

    public static string TrimPrefix(this string source, string prefix)
    {
        if (!source.StartsWith(prefix))
        {
            return source;
        }

        return source.Substring(prefix.Length);
    }

    public static string TrimSuffix(this string source, string suffix)
    {
        if (!source.EndsWith(suffix))
        {
            return source;
        }

        return source.Substring(0, source.Length - suffix.Length);
    }

    public static string ToJson(this object? source)
    {
        if (source == null) return "";

        if (!Try(out var json, out var e, () => JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true })))
        {
            return $"<Error: {e}>";
        }

        return json;
    }

    public static string Txt(this Version? source)
    {
        if (source == null) return "";
        return $"{source.Major}.{source.Minor} ({source.Build}.{source.Revision})";
    }
}