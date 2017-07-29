using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelService : IModelService, IModelNotifications
	{
		private readonly IModelingService modelingService;
		private readonly INodeService nodeService;

		private readonly ILineViewModelService lineViewModelService;

		private readonly Model model;
		private readonly WorkingFolder workingFolder;
		private Dispatcher dispatcher;

		public ModelService(
			IModelingService modelingService,
			INodeService nodeService,
			ILineViewModelService lineViewModelService,
			Model model,
			WorkingFolder workingFolder)
		{
			this.modelingService = modelingService;
			this.nodeService = nodeService;

			this.lineViewModelService = lineViewModelService;
			this.model = model;
			this.workingFolder = workingFolder;
		}


		public void Init(IItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			model.Nodes.Root.ChildrenCanvas = rootCanvas;
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
			foreach (List<DataNode> batch in nodes.Partition(100))
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
			foreach (List<DataLink> batch in links.Partition(100))
			{
				InvokeOnUiThread(UpdateLinks, batch);
			}
		}


		private void UpdateLinks(List<DataLink> batchLinks)
		{
			batchLinks.ForEach(UpdateLink);
		}


		private void UpdateLink(DataLink dataLink)
		{
			NodeId sourceId = new NodeId(new NodeName(dataLink.Source));
			NodeId targetId = new NodeId(new NodeName(dataLink.Target));

			Node source = model.Nodes.Node(sourceId);
			Node target = model.Nodes.Node(targetId);

			Link link = new Link(source, target);
			if (source.Links.Contains(link))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}

			if (source == target)
			{
				// Skipping link to self
				return;
			}

			if (source.Parent != target.Parent)
			{
				return;
			}
		

			IItemsCanvas parentCanvas = source.Parent.ChildrenCanvas;

			LineViewModel lineViewModel = new LineViewModel(
				lineViewModelService, source.ViewModel, target.ViewModel);

			parentCanvas.AddItem(lineViewModel);
		}


		private void InvokeOnUiThread<T>(Action<T> action, T arg)
		{
			dispatcher.Invoke(DispatcherPriority.Background, action, arg);
		}
	}
}