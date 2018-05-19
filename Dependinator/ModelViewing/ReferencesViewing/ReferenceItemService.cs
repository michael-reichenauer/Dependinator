using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Dependinator.Common;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.Lines.Private;
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



		public IEnumerable<ReferenceItem> GetReferences(Line line, ReferenceOptions options)
		{
			Node baseNode = options.IsIncoming ? line.Target : line.Source;
			return GetReferenceItems(new[] { line }, baseNode, options);
		}



		public IEnumerable<ReferenceItem> GetReferences(Node baseNode, ReferenceOptions options)
		{
			IEnumerable<Line> lines =
				(options.IsOutgoing ? baseNode.SourceLines : baseNode.TargetLines)
				.Where(line => line.Owner != baseNode);

			return GetReferenceItems(lines, baseNode, options);
		}



		//IEnumerable<Line> lines =
		//	(options.IsOutgoing ? baseNode.SourceLines : baseNode.TargetLines)
		//	.Where(line => line.Owner != baseNode);

		public IEnumerable<ReferenceItem> GetReferences2(ReferenceOptions2 options)
		{
			IEnumerable<Link> links =
				options.Lines.SelectMany(line => line.Links)
					//.DistinctBy(link => EndPoint2(link, !options.IsSource))
					.Where(link => IsIncluded2(link, options));

			var items = CreateReferenceHierarchy2(links, options);

			if (!items.Any())
			{
				return Enumerable.Empty<ReferenceItem>();
			}

			ReferenceItem rootItem = items[NodeName.Root];

			return rootItem.SubItems;
			//List<ReferenceItem> referenceItems = ReduceHierarchy2(rootItem, options).ToList();
			//referenceItems.ForEach(item => item.Parent = null);
			//return referenceItems;
		}


		private IEnumerable<ReferenceItem> GetReferenceItems(
			IEnumerable<Line> lines, Node baseNode, ReferenceOptions options)
		{
			IEnumerable<Link> lineLinks =
				lines.SelectMany(line => line.Links)
				.DistinctBy(link => EndPoint(link, options))
				.Where(link => IsIncluded(link, options));

			var referenceItems = GetReferenceItems(lineLinks, baseNode, options);

			return referenceItems;
		}


		private static bool IsIncluded(IEdge link, ReferenceOptions options) =>
			options.FilterNode == null ||
			EndPoint(link, options).AncestorsAndSelf().Contains(options.FilterNode);


		private static bool IsIncluded2(IEdge link, ReferenceOptions2 options) =>
			(options.SourceFilter == null || link.Source.AncestorsAndSelf().Contains(options.SourceFilter)) &&
			(options.TargetFilter == null || link.Target.AncestorsAndSelf().Contains(options.TargetFilter));



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
			referenceItems.ForEach(item => item.Parent = null);
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

					item = new ReferenceItem(this, node, options.IsIncoming, baseNode, options.IsSubReference);
					parentItem.AddChild(item);

					items[node.Name] = item;
				}

				item.Link = link;
			}

			return items;
		}

		private Dictionary<NodeName, ReferenceItem> CreateReferenceHierarchy2(
			IEnumerable<Link> links, ReferenceOptions2 options)
		{
			Dictionary<NodeName, ReferenceItem> items = new Dictionary<NodeName, ReferenceItem>();

			foreach (Link link in links)
			{
				Node node = EndPoint2(link, options.IsSource);

				if (!items.TryGetValue(node.Name, out ReferenceItem item))
				{
					ReferenceItem parentItem = GetParentItem2(items, node.Parent, options);

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
			Node baseNode,
			ReferenceOptions options)
		{
			if (items.TryGetValue(parentNode.Name, out ReferenceItem parentItem))
			{
				return parentItem;
			}

			parentItem = new ReferenceItem(this, parentNode, options.IsIncoming, baseNode, options.IsSubReference);

			if (!parentNode.IsRoot)
			{
				ReferenceItem grandParentItem = GetParentItem(items, parentNode.Parent, baseNode, options);
				grandParentItem.AddChild(parentItem);
			}

			items[parentNode.Name] = parentItem;
			return parentItem;
		}



		private ReferenceItem GetParentItem2(
			IDictionary<NodeName, ReferenceItem> items,
			Node parentNode,
			ReferenceOptions2 options)
		{
			if (items.TryGetValue(parentNode.Name, out ReferenceItem parentItem))
			{
				return parentItem;
			}

			parentItem = new ReferenceItem(this, parentNode, false, null, false);

			if (!parentNode.IsRoot)
			{
				ReferenceItem grandParentItem = GetParentItem2(items, parentNode.Parent, options);
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
							ReferenceItem newItem = new ReferenceItem(this, subItem.Node, options.IsIncoming, baseNode, options.IsSubReference)
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

		private IEnumerable<ReferenceItem> ReduceHierarchy2(
			ReferenceItem item, ReferenceOptions2 options)
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
						IEnumerable<ReferenceItem> subItems = ReduceHierarchy2(subItem, options);

						if (subItem.Link != null)
						{
							// Item has a link so we need it and its compressed sub-items
							ReferenceItem newItem = new ReferenceItem(this, subItem.Node, false, null, false)
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


		private static Node EndPoint(IEdge edge, ReferenceOptions options)
		{
			bool isIncoming = options.IsNodes ? !options.IsIncoming : options.IsIncoming;
			return isIncoming ? edge.Source : edge.Target;
		}

		private static Node EndPoint2(IEdge edge, bool isSource)
		{
			return isSource ?  edge.Source : edge.Target;
		}
	}
}