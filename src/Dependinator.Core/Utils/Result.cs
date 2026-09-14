using System.Runtime.CompilerServices;

namespace Dependinator.Core.Utils;

// Every fallible operation returns a Result or a Result<T>: a union of its value (or Success) and an Error,
// matched on the case type. Exceptions are for bugs, not for flow control.
//
//   var result = await git.GetStatusAsync(wd);
//   if (result is not Status status) return result.Error;
//   ... status is a Status from here on
//
//   if (await git.SetValueAsync(key, json, wd) is Error e) return e;
//
//   return await server.PullAsync(name, wd) switch
//   {
//       Success => Result.Ok,
//       Error e => new Error("Failed to pull", e),
//   };
//
//   var tags = await git.GetTagsAsync(wd) is IReadOnlyList<Tag> t ? t : [];
//
// Result and Result<T> are custom unions in the C# 15 sense: a struct with the [Union] attribute, one public
// constructor per case type and an object Value. So a switch over one is exhaustive without a
// discard arm (a missing arm is a build error), and a pattern applies to the contained value rather
// than to the struct. The optional non-boxing members of that pattern (HasValue, TryGetValue) are
// deliberately absent: they are for unions that keep value types unboxed in fields of their own,
// and these store their contents as one object, so the compiler matches on Value, as it does for
// its own union declarations. The attribute is polyfilled while the target framework is net10.0,
// see UnionPolyfill.cs.

// The failure case of Result and Result<T>: a message, where it was created, and optionally the
// one thing it wraps, which is either another Error or an exception.
public class Error
{
    // What this error wraps: an Error, an Exception, or nothing. One field rather than one per
    // kind, so an error can never wrap both, which would leave no order for their messages.
    readonly object? cause;

    public Error(
        string message = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, (object?)null, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        string message,
        Error inner,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, (object)inner, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        string message,
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, (object)exception, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(exception.Message, (object)exception, memberName, sourceFilePath, sourceLineNumber) { }

    Error(string message, object? cause, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        Message = message;
        this.cause = cause;
        Origin = $"{sourceFilePath}({sourceLineNumber}) {memberName}";
    }

    public string Message { get; }

    // The error this one wraps, when that is what it wraps
    public Error? Inner => cause as Error;

    // The exception this one wraps, when that is what it wraps
    public Exception? Exception => cause as Exception;

    // The file, line and member that created the error, since nothing is thrown and so there is no
    // stack trace to tell
    public string Origin { get; }

    // This message and then those of what it wraps, outermost first, which is what a dialog shows
    public string AllMessages() => string.Join(",\n", Messages());

    IEnumerable<string> Messages()
    {
        yield return Message;

        if (cause is Error inner)
        {
            foreach (var message in inner.Messages())
                yield return message;
        }

        // An error created from an exception already has that exception's message as its own
        var exception = cause as Exception;
        if (exception != null && exception.Message == Message)
            exception = exception.InnerException;

        for (; exception != null; exception = exception.InnerException)
            yield return exception.Message;
    }

    public override string ToString() => $"Error: {Message}";
}

// The failure of a lookup whose target does not exist, for the one caller that must tell it apart
// from other failures: a cloud pull answered 404 lets sync-down upload the local model instead.
// Matched with 'is NotFoundError'; it is still an Error to everyone else.
public class NotFoundError : Error
{
    public NotFoundError(
        string message = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base(message, memberName, sourceFilePath, sourceLineNumber) { }
}

// The success case of Result
public sealed class Success
{
    public static readonly Success Instance = new();

    Success() { }

    public override string ToString() => "OK";
}

// The result of an operation that either succeeds or fails: Success or Error
[Union]
public readonly struct Result : IUnion
{
    readonly object? value;

    public Result(Success success) => value = success;

    public Result(Error error) => value = error;

    public static readonly Result Ok = new(Success.Instance);

    // The contained case, for the compiler: every pattern on a result is lowered to a pattern on
    // Value, so it returns the Error as well, and null for default(Result). Match; do not read it.
    public object? Value => value;

    public static implicit operator Result(Error error) => new(error);

    // Runs an action that reports failure by throwing, e.g. a file API, and returns the exception
    // as an error. Every exception is caught, the fatal ones included, since bad input to such an
    // API surfaces as an ArgumentException or an InvalidOperationException.
    //
    //   if (Result.Catch(() => File.Move(source, target)) is Error e) return e;
    public static Result Catch(
        Action action,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        try
        {
            action();
            return Ok;
        }
        catch (Exception e)
        {
            return new Error(e, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    // Runs a function that reports failure by throwing and returns its value, or the exception as
    // an error.
    //
    //   var text = Result.Catch(() => File.ReadAllText(path));
    //   if (text is not string content) return text.Error;
    public static Result<T> Catch<T>(
        Func<T> func,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        where T : notnull
    {
        try
        {
            return func();
        }
        catch (Exception e)
        {
            return new Error(e, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    public override string ToString() =>
        value switch
        {
            Error e => e.ToString(),
            Success => "OK",
            _ => "Unset",
        };
}

// The result of an operation that either produces a value or fails: T or Error
[Union]
public readonly struct Result<T> : IUnion
    where T : notnull
{
    readonly object? value;

    // A null value is not an error, it is a bug in the function returning it
    public Result(T value) =>
        this.value = value ?? throw new InvalidOperationException("A result value cannot be null");

    public Result(Error error) => value = error;

    // The contained case, for the compiler: every pattern on a result is lowered to a pattern on
    // Value, so it returns the Error as well, and null for default(Result<T>). Match rather than
    // read it. The one exception is generic code, which cannot bind a type parameter in a union
    // pattern and matches 'result.Value is T value' instead.
    public object? Value => value;

    // The error of a result already known to be one, i.e. right after a pattern ruled out the
    // value; reading it on a value is a bug in the caller:
    //
    //   if (result is not Status status) return result.Error;
    public Error Error => value as Error ?? throw new InvalidOperationException("Result is not an error");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);

    // Dropping the value keeps the outcome
    public static implicit operator Result(Result<T> result) => result.value is Error e ? new Result(e) : Result.Ok;

    public override string ToString() =>
        value switch
        {
            Error e => e.ToString(),
            null => "Unset",
            _ => value.ToString() ?? "",
        };
}
