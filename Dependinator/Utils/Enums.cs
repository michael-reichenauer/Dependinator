namespace Dependinator.Utils;

public static class Enums
{
    public static TEnum To<TEnum>(string? text)
        where TEnum : struct, Enum
    {
        return Enum.Parse<TEnum>(text ?? "");
    }

    public static TEnum To<TEnum>(string? text, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(text ?? "", out var value) ? value : defaultValue;
    }
}
