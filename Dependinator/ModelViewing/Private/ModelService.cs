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


		public Node Root => model.Root;


		public void Init(ItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			Root.ItemsCanvas = rootCanvas;
		}


		public async Task LoadAsync()
		{
			await modelingService.AnalyzeAsync(workingFolder.FilePath);
		}


		public async Task SaveAsync(string dataFilePath)
		{
			IReadOnlyList<Node> nodes = model.Root.Descendents().ToList();

			IReadOnlyList<DataItem> items = Convert.ToDataItems(nodes);

			await modelingService.SerializeAsync(items, dataFilePath);
		}


		public async Task RefreshAsync(bool refreshLayout)
		{
			await modelingService.AnalyzeAsync(workingFolder.FilePath);
		}


		public void UpdateNodes(IReadOnlyList<DataNode> nodes) =>
			nodes.Partition(BatchSize).ForEach(batch => dispatcher.InvokeBackground(UpdateNodes, batch));


		public void UpdateLinks(IReadOnlyList<DataLink> links) =>
			links.Partition(BatchSize).ForEach(batch => dispatcher.InvokeBackground(UpdateLinks, batch));


		private void UpdateNodes(List<DataNode> nodes) => nodes.ForEach(nodeService.UpdateNode);


		private void UpdateLinks(List<DataLink> links) => links.ForEach(linkService.UpdateLink);
	}
}