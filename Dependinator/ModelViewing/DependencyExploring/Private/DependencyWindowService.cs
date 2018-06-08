using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyWindowService : IDependencyWindowService
	{
		private readonly IDependenciesService dependenciesService;
		private readonly IModelService modelService;
		private readonly Lazy<IModelNotifications> modelNotifications;
		private readonly WindowOwner owner;


		public DependencyWindowService(
			IDependenciesService dependenciesService,
			IModelService modelService,
			Lazy<IModelNotifications> modelNotifications,
			WindowOwner owner)
		{
			this.dependenciesService = dependenciesService;
			this.modelService = modelService;
			this.modelNotifications = modelNotifications;
			this.owner = owner;
		}


		public void ShowCode(NodeName nodeName)
		{
			if (TryGetNode(nodeName, out Node node))
			{
				CodeDialog codeDialog = new CodeDialog(owner, nodeName.DisplayFullName, node.CodeText);
				codeDialog.Show();
			}
			else
			{
				// Node does no longer exists ????
			}
		}



		public bool TryGetNode(NodeName nodeName, out Node node) =>
			modelService.TryGetNode(nodeName, out node);


		public Task RefreshModelAsync() => modelNotifications.Value.ManualRefreshAsync(false);




		public Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			IReadOnlyList<Line> lines, bool isSourceSide, Node sourceNode, Node targetNode) => 
				dependenciesService.GetDependencyItemsAsync(lines, isSourceSide, sourceNode, targetNode);
	}
}