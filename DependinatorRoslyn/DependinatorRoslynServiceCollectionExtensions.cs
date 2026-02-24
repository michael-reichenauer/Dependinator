using DependinatorCore.Parsing.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace DependinatorRoslyn;

public static class DependinatorRoslynServiceCollectionExtensions
{
    public static IServiceCollection AddDependinatorRoslynServices(this IServiceCollection services)
    {
        services.AddTransient<ISourceParser, DependinatorCore.Parsing.Sources.Roslyn.SourceParser>();
        return services;
    }
}
