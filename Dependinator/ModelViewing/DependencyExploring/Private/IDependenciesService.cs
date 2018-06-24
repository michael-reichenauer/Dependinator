using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IDependenciesService
	{
		Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(bool isSource,
			Node sourceNode,
			Node targetNode);
	}
}