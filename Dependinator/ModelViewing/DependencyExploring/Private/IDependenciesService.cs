using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IDependenciesService
	{
		Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter);


		Task<int> GetDependencyCountAsync(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter);
	}
}