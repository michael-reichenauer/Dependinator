using System.Diagnostics.CodeAnalysis;

namespace Dependinator.Core.Utils;

// TEMPORARY, deleted once every call site matches on the union: the six Try signatures of the old
// pre-union result type, over Result/Result<T>, so unconverted call sites keep compiling meanwhile.
// New code matches the result instead: 'if (x is Error e) return e;' and
// 'if (result is not T value) return result.Error;' (see Result.cs).
public static class ResultShim
{
    // Matched on Value rather than 'result is T value': a union pattern whose type is a type
    // parameter cannot declare a variable
    public static bool Try<T>([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out Error? e, Result<T> result)
        where T : notnull
    {
        if (result.Value is T v)
        {
            value = v;
            e = null;
            return true;
        }

        value = default;
        e = result.Error;
        return false;
    }

    public static bool Try([NotNullWhen(false)] out Error? e, Result result)
    {
        if (result is Error error)
        {
            e = error;
            return false;
        }

        e = null;
        return true;
    }

    public static bool Try<T>([NotNullWhen(true)] out T? value, Result<T> result)
        where T : notnull
    {
        if (result.Value is T v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    public static bool Try(Result result) => result is not Error;

    public static bool Try<T>([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out Error? e, Func<T> func)
        where T : notnull => Try(out value, out e, Result.Catch(func));

    public static bool Try([NotNullWhen(false)] out Error? e, Action action) => Try(out e, Result.Catch(action));
}
