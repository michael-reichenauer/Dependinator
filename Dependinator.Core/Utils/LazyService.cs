using Microsoft.Extensions.DependencyInjection;

namespace Dependinator.Core.Utils;

public class LazyService<T> : Lazy<T>
    where T : notnull
{
    public LazyService(IServiceProvider provider)
        : base(provider.GetRequiredService<T>) { }
}
