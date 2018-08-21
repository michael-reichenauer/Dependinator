using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
    internal interface IDependenciesService
    {
        Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(Options options);
    }
}
