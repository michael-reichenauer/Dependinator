using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelService : IModelService, IModelNotifications
	{
		private static readonly int BatchSize = 1000;

		private readonly IModelingService modelingService;
		private readonly INodeService nodeService;
		private readonly ILinkService linkService;


		private readonly Model model;
		private readonly WorkingFolder workingFolder;
		private Dispatcher dispatcher;


		public ModelService(
			IModelingService modelingService,
			INodeService nodeService,
			ILinkService linkService,
			Model model,
			WorkingFolder workingFolder)
		{
			this.modelingService = modelingService;
			this.nodeService = nodeService;
			this.linkService = linkService;

			this.model = model;
			this.workingFolder = workingFolder;
		}


		public void Init(ItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			model.Nodes.Root.ItemsCanvas = rootCanvas;
		}


		public async Task LoadAsync()
		{
			await Task.Run(() => modelingService.Analyze(workingFolder.FilePath));
		}


		public async Task RefreshAsync(bool refreshLayout)
		{

			await Task.Run(() => modelingService.Analyze(workingFolder.FilePath));
		}


		public void UpdateNodes(IReadOnlyList<DataNode> nodes)
		{
			foreach (List<DataNode> batch in nodes.Partition(BatchSize))
			{
				InvokeOnUiThread(UpdateNodes, batch);
			}
		}

		private void UpdateNodes(List<DataNode> batchNodes)
		{
			batchNodes.ForEach(nodeService.UpdateNode);
		}


		public void UpdateLinks(IReadOnlyList<DataLink> links)
		{
			foreach (List<DataLink> batch in links.Partition(BatchSize))
			{
				InvokeOnUiThread(UpdateLinks, batch);
			}
		}


		private void UpdateLinks(List<DataLink> batchLinks)
		{
			batchLinks.ForEach(linkService.UpdateLink);
		}


		private void InvokeOnUiThread<T>(Action<T> action, T arg)
		{
			dispatcher.Invoke(DispatcherPriority.Background, action, arg);
		}
	}
}