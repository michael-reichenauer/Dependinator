using System.Runtime.CompilerServices;

namespace DependinatorCore.Utils;

public class AsyncLazy<TZT> : Lazy<Task<TZT>>
{
    public AsyncLazy(Func<TZT> valueFactory)
        : base(() => Task.Factory.StartNew(valueFactory)) { }

    public AsyncLazy(Func<Task<TZT>> taskFactory)
        : base(() => Task.Factory.StartNew(() => taskFactory()).Unwrap()) { }

    public TaskAwaiter<TZT> GetAwaiter()
    {
        return Value.GetAwaiter();
    }
}

public class AsyncLazyX<TZT, TR> : Lazy<Task<TZT>>
{
    public AsyncLazyX(Func<TZT> valueFactory)
        : base(() => Task.Factory.StartNew(valueFactory)) { }

    public AsyncLazyX(Func<Task<TZT>> taskFactory)
        : base(() => Task.Factory.StartNew(() => taskFactory()).Unwrap()) { }

    public TaskAwaiter<TZT> GetAwaiter()
    {
        return Value.GetAwaiter();
    }
}
