using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring
{
	internal interface IDependenciesService
	{
		Task<IReadOnlyList<DependencyItem>> GetReferencesAsync(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter);
	}
}