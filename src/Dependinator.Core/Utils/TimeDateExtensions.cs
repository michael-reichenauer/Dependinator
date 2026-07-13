namespace Dependinator.Core.Utils;

public static class TimeDateExtensions
{
    public static string IsoZone(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss zzz}";
    }
}
