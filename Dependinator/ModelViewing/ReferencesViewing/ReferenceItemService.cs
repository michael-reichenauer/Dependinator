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


		public IEnumerable<ReferenceItem> GetSourceLinkItems(Line line) => 
			GetLineReferenceItems(line, true);


		public IEnumerable<ReferenceItem> GetTargetLinkItems(Line line) => 
			GetLineReferenceItems(line, false);


		public IEnumerable<ReferenceItem> GetIncomingReferences(Node node)
		{
			IEnumerable<Line> lines = node.TargetLines
				.Where(line => line.Owner != node);

			return lines.SelectMany(line1 => GetLineReferenceItems(line1, true));
		}


		public IEnumerable<ReferenceItem> GetOutgoingReferences(Node node)
		{
			IEnumerable<Line> lines = node.SourceLines
				.Where(line => line.Owner != node);

			return lines.SelectMany(line1 => GetLineReferenceItems(line1, false));
		}


		private IEnumerable<ReferenceItem> GetLineReferenceItems(
			Line line, bool isIncoming)
		{
			IEnumerable<Link> lineLinks = line.Links.DistinctBy(link => EndPoint(link, isIncoming));

			var referenceItems = GetReferenceItems(lineLinks, isIncoming);

			return referenceItems;
		}


		private IEnumerable<ReferenceItem> GetReferenceItems(IEnumerable<Link> links, bool isIncoming)
		{
			var items = CreateReferenceHierarchy(links, isIncoming);

			ReferenceItem rootItem = items[NodeName.Root];

			List<ReferenceItem> referenceItems = ReduceHierarchy(rootItem, isIncoming).ToList();

			return referenceItems;
		}


		private Dictionary<NodeName, ReferenceItem> CreateReferenceHierarchy(
			IEnumerable<Link> links, bool isIncoming)
		{
			Dictionary<NodeName, ReferenceItem> items = new Dictionary<NodeName, ReferenceItem>();

			foreach (Link link in links)
			{
				Node node = EndPoint(link, isIncoming);

				if (!items.TryGetValue(node.Name, out ReferenceItem item))
				{
					ReferenceItem parentItem = GetParentItem(items, node.Parent, isIncoming);

					item = new ReferenceItem(this, node, isIncoming);
					parentItem.AddChild(item);

					items[node.Name] = item;
				}

				item.Link = link;
			}

			return items;
		}


		private ReferenceItem GetParentItem(
			IDictionary<NodeName, ReferenceItem> items, Node parentNode, bool isIncoming)
		{
			if (items.TryGetValue(parentNode.Name, out ReferenceItem parentItem))
			{
				return parentItem;
			}

			parentItem = new ReferenceItem(this, parentNode, isIncoming);

			if (!parentNode.IsRoot)
			{
				ReferenceItem grandParentItem = GetParentItem(items, parentNode.Parent, isIncoming);
				grandParentItem.AddChild(parentItem);
			}

			items[parentNode.Name] = parentItem;
			return parentItem;
		}


		/// <summary>
		/// Many nodes, just contain one child, lets reduce the hierarchy by replace such
		/// parents with their children 
		/// </summary>
		private IEnumerable<ReferenceItem> ReduceHierarchy(ReferenceItem item, bool isIncoming)
		{
			foreach (var subItem in item.Items)
			{
				if (subItem.Items.Any())
				{
					if (subItem.Items.Count > 1)
					{
						// 2 or more sub items, should not be reduced
						yield return subItem;
					}
					else
					{
						// 1 sub item which might be reduced
						IEnumerable<ReferenceItem> subItems = ReduceHierarchy(subItem, isIncoming);

						if (subItem.Link != null)
						{
							// Item has a link so we need it and its compressed sub-items
							ReferenceItem newItem = new ReferenceItem(this, subItem.Node, isIncoming)
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


		private Node EndPoint(IEdge edge, bool isIncoming) =>
			isIncoming ? edge.Source : edge.Target;
	}
}