using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Dependinator.Core.Utils;

public class FatalExceptionEventArgs : EventArgs
{
    public string Message { get; }

    public Exception Exception { get; }

    public FatalExceptionEventArgs(string message, Exception exception)
    {
        Message = message;
        Exception = exception;
    }
}

public static class FatalExceptionsExtensions
{
    // Exception types that indicate programmer errors or corrupted process state. Used by
    // "catch (Exception e) when (e.IsNotFatal())" filters so these are never swallowed;
    // they are reported via the FatalException event (which shuts the app down) instead.
    // Derived types count as fatal too (e.g. ArgumentNullException, ObjectDisposedException).
    private static readonly HashSet<Type> FatalTypes = new()
    {
        typeof(ArgumentException),
        typeof(AccessViolationException),
        typeof(AppDomainUnloadedException),
        typeof(ArithmeticException),
        typeof(ArrayTypeMismatchException),
        typeof(BadImageFormatException),
        typeof(CannotUnloadAppDomainException),
        typeof(ContextMarshalException),
        typeof(DataMisalignedException),
        typeof(IndexOutOfRangeException),
        typeof(InsufficientExecutionStackException),
        typeof(InvalidCastException),
        typeof(InvalidOperationException),
        typeof(InvalidProgramException),
        typeof(MemberAccessException),
        typeof(MulticastNotSupportedException),
        typeof(NotImplementedException),
        typeof(NotSupportedException),
        typeof(NullReferenceException),
        typeof(OutOfMemoryException),
        typeof(RankException),
        typeof(AmbiguousMatchException),
        typeof(InvalidComObjectException),
        typeof(InvalidOleVariantTypeException),
        typeof(MarshalDirectiveException),
        typeof(SafeArrayRankMismatchException),
        typeof(SafeArrayTypeMismatchException),
        typeof(StackOverflowException),
        typeof(TypeInitializationException),
    };

    public static event EventHandler<FatalExceptionEventArgs>? FatalException;

    public static bool IsNotFatal(this Exception e)
    {
        if (!IsFatalType(e.GetType()))
        {
            return true;
        }

        StackTrace stackTrace = new StackTrace(1, true);
        string message = $"Exception type is fatal: {e.GetType()}, {e}\n at \n{stackTrace}";

        FatalException?.Invoke(null, new FatalExceptionEventArgs(message, e));
        return false;
    }

    static bool IsFatalType(Type exceptionType)
    {
        for (Type? type = exceptionType; type != null; type = type.BaseType)
        {
            if (FatalTypes.Contains(type))
            {
                return true;
            }
        }

        return false;
    }
}
