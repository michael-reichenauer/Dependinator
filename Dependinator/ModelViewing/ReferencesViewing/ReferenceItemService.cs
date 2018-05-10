using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Dependinator.Common;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemService : IReferenceItemService
	{
		private readonly IThemeService themeService;


		public Brush ItemTextBrush() => themeService.GetTextBrush();

		public Brush ItemTextHiddenBrush() => themeService.GetTextDimBrush();


		public ReferenceItemService(IThemeService themeService)
		{
			this.themeService = themeService;
		}


		//public IEnumerable<ReferenceItem> GetSourceLinkItems(Line line, Node baseNode) =>
		//	GetLineReferenceItems(line, true, baseNode, null);


		//public IEnumerable<ReferenceItem> GetTargetLinkItems(Line line, Node baseNode) =>
		//	GetLineReferenceItems(line, false, baseNode, null);


		//public IEnumerable<ReferenceItem> GetIncomingReferences(Node baseNode, ReferenceOptions options)
		//{
		//	IEnumerable<Line> lines = baseNode.TargetLines
		//		.Where(line => line.Owner != baseNode);

		//	return lines.SelectMany(line => GetLineReferenceItems(line, baseNode, options));
		//}


		public IEnumerable<ReferenceItem> GetReferences(Node baseNode, ReferenceOptions options)
		{
			IEnumerable<Line> lines = 
				(options.IsIncoming ? baseNode.TargetLines : baseNode.SourceLines)
				.Where(line => line.Owner != baseNode);

			return lines.SelectMany(line => GetLineReferenceItems(line, baseNode, options));
		}


		private IEnumerable<ReferenceItem> GetLineReferenceItems(
			Line line, Node baseNode, ReferenceOptions options)
		{
			IEnumerable<Link> lineLinks = line.Links
				.DistinctBy(link => EndPoint(link, options))
				.Where(link => IsIncluded(link, options));

			var referenceItems = GetReferenceItems(lineLinks, baseNode, options);

			return referenceItems;
		}


		private static bool IsIncluded(IEdge link, ReferenceOptions options) => 
			options.FilterNode == null || 
			EndPoint(link, options).AncestorsAndSelf().Contains(options.FilterNode);


		private IEnumerable<ReferenceItem> GetReferenceItems(
			IEnumerable<Link> links, Node baseNode, ReferenceOptions options)
		{
			var items = CreateReferenceHierarchy(links, baseNode, options);

			if (!items.Any())
			{
				return Enumerable.Empty<ReferenceItem>();
			}

			ReferenceItem rootItem = items[NodeName.Root];

			List<ReferenceItem> referenceItems = ReduceHierarchy(rootItem, baseNode, options).ToList();

			return referenceItems;
		}


		private Dictionary<NodeName, ReferenceItem> CreateReferenceHierarchy(
			IEnumerable<Link> links, Node baseNode, ReferenceOptions options)
		{
			Dictionary<NodeName, ReferenceItem> items = new Dictionary<NodeName, ReferenceItem>();

			foreach (Link link in links)
			{
				Node node = EndPoint(link, options);

				if (!items.TryGetValue(node.Name, out ReferenceItem item))
				{
					ReferenceItem parentItem = GetParentItem(items, node.Parent, baseNode, options);

					item = new ReferenceItem(this, node, options.IsIncoming, baseNode);
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
			Node baseNode,
			ReferenceOptions options)
		{
			if (items.TryGetValue(parentNode.Name, out ReferenceItem parentItem))
			{
				return parentItem;
			}

			parentItem = new ReferenceItem(this, parentNode, options.IsIncoming, baseNode);

			if (!parentNode.IsRoot)
			{
				ReferenceItem grandParentItem = GetParentItem(items, parentNode.Parent, baseNode, options);
				grandParentItem.AddChild(parentItem);
			}

			items[parentNode.Name] = parentItem;
			return parentItem;
		}


		/// <summary>
		/// Many nodes, just contain one child, lets reduce the hierarchy by replace such
		/// parents with their children 
		/// </summary>
		private IEnumerable<ReferenceItem> ReduceHierarchy(
			ReferenceItem item, Node baseNode, ReferenceOptions options)
		{
			foreach (var subItem in item.SubItems)
			{
				if (subItem.SubItems.Any())
				{
					if (subItem.SubItems.Count > 1)
					{
						// 2 or more sub items, should not be reduced
						yield return subItem;
					}
					else
					{
						// 1 sub item which might be reduced
						IEnumerable<ReferenceItem> subItems = ReduceHierarchy(subItem, baseNode, options);

						if (subItem.Link != null)
						{
							// Item has a link so we need it and its compressed sub-items
							ReferenceItem newItem = new ReferenceItem(this, subItem.Node, options.IsIncoming, baseNode)
							{
								Link = subItem.Link
							};

							newItem.AddChildren(subItems);
							yield return newItem;
						}
						else
						{
							// Replace this item and use its sub-items
							foreach (ReferenceItem subSubItem in subItems)
							{
								yield return subSubItem;
							}
						}
					}
				}
				else if (subItem.Link != null)
				{
					// a leaf item with link, but no sub items
					yield return subItem;
				}
			}
		}


		private static Node EndPoint(IEdge edge, ReferenceOptions options ) =>
			options.IsIncoming ? edge.Source : edge.Target;
	}
}