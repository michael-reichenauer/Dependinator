using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Dependinator.Common;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemService : IReferenceItemService
	{
		private readonly IThemeService themeService;
		private readonly WindowOwner owner;


		public ReferenceItemService(
			IThemeService themeService,
			WindowOwner owner)
		{
			this.themeService = themeService;
			this.owner = owner;
		}


		public Brush ItemTextBrush() => themeService.GetTextBrush();

		public Brush ItemTextLowBrush() => themeService.GetTextLowBrush();


		public void ShowCode(Node node)
		{
			CodeDialog codeDialog = new CodeDialog(owner, node);
			codeDialog.Show();
		}


		public Brush ItemTextHiddenBrush() => themeService.GetTextDimBrush();



		public IEnumerable<ReferenceItem> GetReferences(
			IEnumerable<Line> lines, ReferenceOptions options)
		{
			IEnumerable<Link> links = lines
				.SelectMany(line => line.Links)
				.Where(link => IsIncluded(link, options));

			var items = CreateReferenceHierarchy(links, options);

			if (!items.Any())
			{
				return Enumerable.Empty<ReferenceItem>();
			}

			ReferenceItem rootItem = items[NodeName.Root];

			return rootItem.SubItems;
		}



		private static bool IsIncluded(IEdge link, ReferenceOptions options) =>
			(options.SourceFilter == null || link.Source.AncestorsAndSelf().Contains(options.SourceFilter)) &&
			(options.TargetFilter == null || link.Target.AncestorsAndSelf().Contains(options.TargetFilter));



		private Dictionary<NodeName, ReferenceItem> CreateReferenceHierarchy(
			IEnumerable<Link> links, ReferenceOptions options)
		{
			Dictionary<NodeName, ReferenceItem> items = new Dictionary<NodeName, ReferenceItem>();

			foreach (Link link in links)
			{
				Node node = EndPoint(link, options.IsSource);

				if (!items.TryGetValue(node.Name, out ReferenceItem item))
				{
					ReferenceItem parentItem = GetParentItem(items, node.Parent, options);

					item = new ReferenceItem(this, node, false, null, false);
					parentItem.AddChild(item);

					items[node.Name] = item;
				}

				item.Link = link;
			}

			return items;
		}


		private ReferenceItem GetParentItem(
			IDictionary<NodeName, ReferenceItem> items,
			Node parentNode,
			ReferenceOptions options)
		{
			if (items.TryGetValue(parentNode.Name, out ReferenceItem parentItem))
			{
				return parentItem;
			}

			parentItem = new ReferenceItem(this, parentNode, false, null, false);

			if (!parentNode.IsRoot)
			{
				ReferenceItem grandParentItem = GetParentItem(items, parentNode.Parent, options);
				grandParentItem.AddChild(parentItem);
			}

			items[parentNode.Name] = parentItem;
			return parentItem;
		}


		private static Node EndPoint(IEdge edge, bool isSource)
		{
			return isSource ?  edge.Source : edge.Target;
		}
	}
}