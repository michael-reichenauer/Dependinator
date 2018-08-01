using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;


namespace System
{
    /// <summary>
    ///     Provides extension methods for <see cref="Exception" />.
    ///     Based on: https://github.com/aelij/AsyncFriendlyStackTrace
    /// </summary>
    public static class ExceptionAsyncExtensions
    {
        private const string EndOfInnerExceptionStack = "--- End of inner exception stack trace ---";
        private const string AggregateExceptionFormatString = "{0}{1}---> (Inner Exception #{2}) {3}{4}{5}";
        private const string AsyncStackTraceExceptionData = "AsyncFriendlyStackTrace";

        private static readonly Func<Exception, string> GetRemoteStackTraceString =
            GenerateGetField<Exception, string>("_remoteStackTraceString");

        private static Func<Exception, string> GetStackTraceString =>
            GenerateGetField<Exception, string>(IsRunningOnMono() ? "stack_trace" : "_stackTraceString");


        /// <summary>
        ///     Gets an async-friendly <see cref="Exception" /> string using <see cref="StackTraceExtensions.ToAsyncString" />.
        ///     Includes special handling for <see cref="AggregateException" />s.
        /// </summary>
        /// <param name="exception">The exception to format.</param>
        /// <returns>An async-friendly string representation of an <see cref="Exception" />.</returns>
        public static string ToAsyncString(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            var innerExceptions = GetInnerExceptions(exception);
            if (innerExceptions != null)
            {
                return ToAsyncAggregateString(exception, innerExceptions);
            }

            return ToAsyncStringCore(exception, false);
        }


        /// <summary>
        ///     Prepares an <see cref="Exception" /> for serialization by including the async-friendly
        ///     stack trace as additional <see cref="Exception.Data" />.
        ///     Note that both the original and the new stack traces will be serialized.
        ///     This method operates recursively on all inner exceptions,
        ///     including ones in an <see cref="AggregateException" />.
        /// </summary>
        /// <param name="exception">The exception.</param>
        public static void PrepareForAsyncSerialization(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            if (exception.Data[AsyncStackTraceExceptionData] != null ||
                GetStackTraceString(exception) != null)
                return;

            exception.Data[AsyncStackTraceExceptionData] = GetAsyncStackTrace(exception);

            var innerExceptions = GetInnerExceptions(exception);
            if (innerExceptions != null)
            {
                foreach (var innerException in innerExceptions)
                {
                    innerException.PrepareForAsyncSerialization();
                }
            }
            else
            {
                exception.InnerException?.PrepareForAsyncSerialization();
            }
        }


        private static IList<Exception> GetInnerExceptions(Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                return aggregateException.InnerExceptions;
            }

            if (exception is ReflectionTypeLoadException reflectionTypeLoadException)
            {
                return reflectionTypeLoadException.LoaderExceptions;
            }

            return null;
        }


        private static string ToAsyncAggregateString(Exception exception, IList<Exception> inner)
        {
            var s = ToAsyncStringCore(exception, true);
            for (var i = 0; i < inner.Count; i++)
            {
                s = string.Format(CultureInfo.InvariantCulture, AggregateExceptionFormatString, s,
                    Environment.NewLine, i, inner[i].ToAsyncString(), "<---", Environment.NewLine);
            }

            return s;
        }


        private static string ToAsyncStringCore(Exception exception, bool includeMessageOnly)
        {
            var message = exception.Message;
            var className = exception.GetType().ToString();
            var s = message.Length <= 0 ? className : className + ": " + message;

            var innerException = exception.InnerException;
            if (innerException != null)
            {
                if (includeMessageOnly)
                {
                    do
                    {
                        s += " ---> " + innerException.Message;
                        innerException = innerException.InnerException;
                    } while (innerException != null);
                }
                else
                {
                    s += " ---> " + innerException.ToAsyncString() + Environment.NewLine +
                         "   " + EndOfInnerExceptionStack;
                }
            }

            s += Environment.NewLine + GetAsyncStackTrace(exception);

            return s;
        }


        private static string GetAsyncStackTrace(Exception exception)
        {
            var stackTrace = exception.Data[AsyncStackTraceExceptionData] ??
                             GetStackTraceString(exception) ??
                             new StackTrace(exception, true).ToAsyncString();
            var remoteStackTrace = GetRemoteStackTraceString(exception);
            return remoteStackTrace + stackTrace;
        }


        /// <summary>
        ///     Allows accessing private fields efficiently.
        /// </summary>
        /// <typeparam name="TOwner">Type of the field's owner.</typeparam>
        /// <typeparam name="TField">Type of the field.</typeparam>
        /// <param name="fieldName">The field name.</param>
        /// <returns>A delegate field accessor.</returns>
        private static Func<TOwner, TField> GenerateGetField<TOwner, TField>(string fieldName)
        {
            var param = Expression.Parameter(typeof(TOwner));
            return Expression.Lambda<Func<TOwner, TField>>(Expression.Field(param, fieldName), param).Compile();
        }


        // see http://www.mono-project.com/docs/gui/winforms/porting-winforms-applications/#runtime-conditionals
        internal static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }
    }
}
