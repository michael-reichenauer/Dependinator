using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IDependencyWindowService
	{
		bool TryGetNode(NodeName nodeName, out Node node);
		Task RefreshModelAsync();
		Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(IReadOnlyList<Line> lines, bool isSourceSide,
			Node sourceNode, Node targetNode);


		void ShowCode(NodeName nodeName);
	}
}