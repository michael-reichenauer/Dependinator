namespace DependinatorCore.Utils;

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

    public static TEnum? ToOrNull<TEnum>(string? text)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(text ?? "", out var value) ? value : null;
    }

    public static string Name<TEnum>(this TEnum value)
        where TEnum : struct, Enum => Enum.GetName(value) ?? value.ToString();
}
