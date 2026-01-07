namespace DependinatorCore.Utils;

public static class TimeDateExtensions
{
    public static string Iso(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss}";
    }

    public static string IsoMs(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss.fff}";
    }

    public static string IsoZone(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss zzz}";
    }

    public static string IsoDate(this DateTime source)
    {
        return $"{source:yyyy-MM-dd}";
    }
}
