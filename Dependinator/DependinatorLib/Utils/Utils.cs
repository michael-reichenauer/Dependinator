public static class Utils
{
    public static bool HasAttribute(this Type type, Type attributeType)
    {
        return type.IsDefined(attributeType, inherit: true);
    }

    public static bool HasAttribute<T>(this Type type) where T : Attribute
    {
        return type.HasAttribute(typeof(T));
    }
}