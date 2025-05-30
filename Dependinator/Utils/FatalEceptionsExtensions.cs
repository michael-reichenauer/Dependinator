using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace System;

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
    private static readonly IReadOnlyList<Type> FatalTypes = new[]
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
        Exception exception = e;
        Type exceptionType = e.GetType();

        if (FatalTypes.Contains(exceptionType))
        {
            StackTrace stackTrace = new StackTrace(1, true);
            string stackTraceText = stackTrace.ToString();
            string message = $"Exception type is fatal: {exceptionType}, {e}\n at \n{stackTraceText}";

            FatalException?.Invoke(null, new FatalExceptionEventArgs(message, exception));
            return false;
        }

        return true;
    }
}
