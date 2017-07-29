using Dependinator.Modeling;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Links
{
	internal class LinkService : ILinkService
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly Model model;

		public LinkService(
			ILineViewModelService lineViewModelService,
			Model model)
		{
			this.lineViewModelService = lineViewModelService;
			this.model = model;
		}

		public void UpdateLink(DataLink dataLink)
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

	}
}