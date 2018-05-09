using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemService : IReferenceItemService
	{
		private static readonly string MoreText = "...";
		private static readonly int LinksMenuLimit = 35;

		private static readonly IEnumerable<ReferenceItemViewModel> EmptySubLinks =
			Enumerable.Empty<ReferenceItemViewModel>();


		public IEnumerable<ReferenceItemViewModel> GetSourceLinkItems(Line line)
		{
			return GetLineLinkItems(line, link => link.Source);
		}


		public IEnumerable<ReferenceItemViewModel> GetTargetLinkItems(Line line)
		{
			return GetLineLinkItems(line, link => link.Target);
		}


		public IEnumerable<ReferenceItemViewModel> GetSourceLinkItems(IEnumerable<Line> lines)
		{
			return GetLinkItems(lines, line => line.Source);
		}


		public IEnumerable<ReferenceItemViewModel> GetTargetLinkItems(IEnumerable<Line> lines)
		{
			return GetLinkItems(lines, line => line.Target);
		}


		public IEnumerable<ReferenceItemViewModel> GetLinkItems(
			IEnumerable<Line> lines,
			Func<IEdge, Node> endPoint)
		{
			List<ReferenceItemViewModel> lineItems = new List<ReferenceItemViewModel>();

			foreach (Line line in lines)
			{
				IEnumerable<ReferenceItemViewModel> linkItems = GetLineLinkItems(line, endPoint);

				//if (line.IsToChild)
				{
					lineItems.AddRange(linkItems);
				}
				//else
				//{
				//	// Line to parent or sibling
				//	string nodeName = endPoint(line).Name.DisplayName;
				//	ReferenceItemViewModel lineMenuItemViewModel = new ReferenceItemViewModel(
				//		null, nodeName, linkItems);

				//	lineItems.Add(lineMenuItemViewModel);
				//}
			}

			return lineItems;
		}


		//public IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(IEnumerable<Line> lines)
		//{
		//	Func<Line, Node> lineEndPoint = line => line.Target;
		//	Func<Link, Node> linkEndPoint = link => link.Target;

		//	List<LineMenuItemViewModel> lineItems = new List<LineMenuItemViewModel>();

		//	foreach (Line line in lines)
		//	{
		//		IEnumerable<LineMenuItemViewModel> linkItems = GetLineLinkItems(line, linkEndPoint);
		//		if (line.Source.Parent != line.Target)
		//		{
		//			string displayName = lineEndPoint(line).Name.DisplayName;
		//			LineMenuItemViewModel lineMenuItemViewModel = new LineMenuItemViewModel(null, displayName, linkItems);

		//			lineItems.Add(lineMenuItemViewModel);
		//		}
		//		else
		//		{
		//			lineItems.AddRange(linkItems);
		//		}

		//	}

		//	return lineItems;
		//}


		//private IEnumerable<LinkItem> GetLinesLinkItems(
		//	IEnumerable<Line> lines, Func<Line, Node> lineEndPoint, Func<Link, Node> linkEndPoint)
		//{
		//	List<LinkItem> lineItems = new List<LinkItem>();
		//	foreach (Line line in lines)
		//	{
		//		IEnumerable<LinkItem> linkItems = GetLineLinkItems(line, linkEndPoint);
		//		string displayName = lineEndPoint(line).Name.DisplayName;
		//		LinkItem linkItem = new LinkItem(null, displayName, linkItems);

		//		lineItems.Add(linkItem);
		//	}

		//	return lineItems;
		//}


		private IEnumerable<ReferenceItemViewModel> GetLineLinkItems(
			Line line, Func<IEdge, Node> endPoint)
		{
			IEnumerable<Link> lineLinks = line.Links.DistinctBy(link => endPoint(link));

			return GetLinkItems2(lineLinks, endPoint);
		}


		private IEnumerable<ReferenceItemViewModel> GetLinkItems2(
			IEnumerable<Link> links, Func<IEdge, Node> endPoint)
		{
			Dictionary<NodeName, MenuItem> items = new Dictionary<NodeName, MenuItem>();

			foreach (Link link in links)
			{
				Node node = endPoint(link);

				if (!items.TryGetValue(node.Name, out MenuItem item))
				{
					MenuItem parentItem = GetParentMenuItem(items, node.Parent);

					item = new MenuItem(node);
					items[node.Name] = item;

					parentItem.Items.Add(item);
				}

				item.Link = link;
			}

			MenuItem rootItem = items[NodeName.Root];
			IEnumerable<MenuItem> compressedItems = CompressSubItems(rootItem);

			return ToLineMenuItemViewModels(compressedItems);
		}


		private static IEnumerable<ReferenceItemViewModel> ToLineMenuItemViewModels(
			IEnumerable<MenuItem> compressedItems)
		{
			return compressedItems
				.Select(item => new ReferenceItemViewModel(
					item.Link, item.Node.Name.DisplayFullNoParametersName, ToLineMenuItemViewModels(item.Items)));
		}


		private IEnumerable<MenuItem> CompressSubItems(MenuItem item)
		{
			foreach (var subItem in item.Items)
			{
				if (subItem.Items.Any())
				{
					if (subItem.Items.Count > 1)
					{
						// 2 or more sub items, should not be compressed
						yield return subItem;
					}
					else
					{
						// 1 sub item which might be compressed
						IEnumerable<MenuItem> subItems = CompressSubItems(subItem);

						if (subItem.Link != null)
						{
							// Item has a link so we need it and its compressed subitems
							MenuItem newItem = new MenuItem(subItem.Node) { Link = subItem.Link };
							newItem.Items.AddRange(subItems);
							yield return newItem;
						}
						else
						{
							// Skip this item and use its compressed subitems
							foreach (MenuItem subSubItem in subItems)
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



		private MenuItem GetParentMenuItem(IDictionary<NodeName, MenuItem> items, Node parent)
		{
			if (items.TryGetValue(parent.Name, out MenuItem parentItem))
			{
				return parentItem;
			}

			parentItem = new MenuItem(parent);

			if (!parent.IsRoot)
			{
				MenuItem grandParentIItem = GetParentMenuItem(items, parent.Parent);
				grandParentIItem.Items.Add(parentItem);
			}

			items[parent.Name] = parentItem;
			return parentItem;
		}


		private class MenuItem
		{
			public MenuItem(Node node) => Node = node;

			public Node Node { get; }
			public Link Link { get; set; }
			public List<MenuItem> Items { get; } = new List<MenuItem>();
			public override string ToString() => $"{Node}";
		}


		private IEnumerable<ReferenceItemViewModel> GetLinkItems(
			IEnumerable<Link> links, Func<IEdge, Node> endPoint, int level)
		{
			if (!links.Any())
			{
				return new List<ReferenceItemViewModel>();
			}
			else if (links.Count() < LinksMenuLimit)
			{
				return links.Select(link => new ReferenceItemViewModel(
					link, GetLinkText(endPoint(link)), EmptySubLinks));
			}

			var groups = links
				.GroupBy(link => GetGroupKey(endPoint(link), level))
				.OrderBy(group => group.Key);

			List<ReferenceItemViewModel> linkItems = GetLinksGroupsItems(groups, endPoint, level);

			//ReduceHierarchy(linkItems);

			return linkItems.OrderBy(item => item, LinkItemComparer());
		}


		private static Comparer<ReferenceItemViewModel> LinkItemComparer()
		{
			return Comparer<ReferenceItemViewModel>.Create(CompareLinkItems);
		}


		private static int CompareLinkItems(ReferenceItemViewModel i1, ReferenceItemViewModel i2)
			=> i1.Name == MoreText && i2.Name == MoreText
				? 0
				: i1.Name == MoreText
					? 1
					: i2.Name == MoreText
						? -1
						: i1.Name.CompareTo(i2.Name);


		private static void ReduceHierarchy(ICollection<ReferenceItemViewModel> linkItems)
		{
			int margin = Math.Max(LinksMenuLimit - linkItems.Count, 0);

			while (margin >= 0)
			{
				bool isChanged = false;

				foreach (ReferenceItemViewModel linkItem in linkItems.Where(item => item.Items.Any()).ToList())
				{
					int count = linkItem.Items.Count();

					if (count > 4)
					{
						continue;
					}

					margin -= (count - 1);

					if (margin >= 0)
					{
						linkItems.Remove(linkItem);
						linkItem.Items.ForEach(linkItems.Add);
						isChanged = true;
					}
				}

				if (!isChanged)
				{
					break;
				}
			}
		}


		private List<ReferenceItemViewModel> GetLinksGroupsItems(
			IEnumerable<IGrouping<string, Link>> linksGroups, Func<IEdge, Node> endPoint, int level)
		{
			var linkItems = linksGroups
				.Take(LinksMenuLimit)
				.Select(group => GetLinkItem(group, endPoint, level));

			if (linksGroups.Count() > LinksMenuLimit)
			{
				List<ReferenceItemViewModel> moreLinks = GetLinksGroupsItems(linksGroups.Skip(LinksMenuLimit), endPoint, level);

				linkItems = linkItems.Concat(new[] { new ReferenceItemViewModel(null, MoreText, moreLinks) });
			}

			return linkItems.ToList();
		}


		private ReferenceItemViewModel GetLinkItem(
			IGrouping<string, Link> linksGroup, Func<IEdge, Node> endPoint, int level)
		{
			IEnumerable<ReferenceItemViewModel> subLinks = GetLinkItems(linksGroup, endPoint, level + 1);

			ReferenceItemViewModel itemViewModel = new ReferenceItemViewModel(null, GetGroupText(linksGroup.Key), subLinks);

			return itemViewModel;
		}


		private static string GetGroupText(string key)
		{
			string[] parts = key.Split(".".ToCharArray());
			string fullName = string.Join(".", parts
				.Where(part => !part.StartsWithTxt("$") && !part.StartsWithTxt("?")));

			if (string.IsNullOrEmpty(fullName))
			{
				fullName = key;
			}

			return NodeName.ToNiceText(fullName);
		}


		private static string GetLinkText(Node node)
		{
			return node.Name.DisplayFullNoParametersName;
		}

		string GetGroupKey(Node node, int level)
		{
			string[] parts = node.Name.FullName.Split(".".ToCharArray());
			return string.Join(".", parts.Take(level));
		}
	}
}