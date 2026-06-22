using System.Text;

namespace Shared;

public static class BlobNameSanitizer
{
    public static string SanitizeForBlobName(
        string value,
        string? fallbackValue = null,
        string parameterName = "value",
        bool makeLowerCase = false
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                builder.Append('-');
            }
            else if (character is '<' or '>' or ':' or '\"' or '/' or '\\' or '|' or '?' or '*' or '\0')
            {
                builder.Append('-');
            }
            else
            {
                builder.Append(character);
            }
        }

        string sanitizedValue = builder.ToString().Trim('.');
        if (makeLowerCase)
            sanitizedValue = sanitizedValue.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(sanitizedValue))
            return sanitizedValue;

        if (fallbackValue is not null)
            return fallbackValue;

        throw new ArgumentException("Value does not produce a valid blob name.", parameterName);
    }
}
