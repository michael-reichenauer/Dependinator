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
		//private static readonly string MoreText = "...";
		//private static readonly int LinksMenuLimit = 35;

		//private static readonly IEnumerable<ReferenceItemViewModel> EmptySubLinks =
		//	Enumerable.Empty<ReferenceItemViewModel>();



		public Brush ItemTextBrush() => themeService.GetTextBrush();

		public Brush ItemTextHiddenBrush() => themeService.GetTextDimBrush();


		public ReferenceItemService(IThemeService themeService)
		{
			this.themeService = themeService;
		}

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
			return ReferenceItemViewModelItems(lines, line => line.Source);
		}


		public IEnumerable<ReferenceItemViewModel> GetOutgoingReferences(Node node)
		{
			IEnumerable<Line> lines = node.SourceLines
				.Where(line => line.Owner != node);

			return ReferenceItemViewModelItems(lines, line => line.Target);
		}


		private IEnumerable<ReferenceItemViewModel> ReferenceItemViewModelItems(
			IEnumerable<Line> lines,
			Func<IEdge, Node> endPoint)
		{
			List<ReferenceItemViewModel> lineItems = new List<ReferenceItemViewModel>();

			foreach (Line line in lines)
			{
				IEnumerable<ReferenceItemViewModel> linkItems = GetLineLinkItems(line, endPoint);

				lineItems.AddRange(linkItems);
			}

			return lineItems;
		}


		private IEnumerable<ReferenceItemViewModel> GetLineLinkItems(
			Line line, Func<IEdge, Node> endPoint)
		{
			IEnumerable<Link> lineLinks = line.Links.DistinctBy(link => endPoint(link));

			var referenceItems = GetReferenceItemItems(lineLinks, endPoint);

			return ToReferenceItemViewModels(referenceItems);
		}


		private IEnumerable<ReferenceItem> GetReferenceItemItems(
			IEnumerable<Link> links,
			Func<IEdge, Node> endPoint)
		{
			Dictionary<NodeName, ReferenceItem> items = new Dictionary<NodeName, ReferenceItem>();

			foreach (Link link in links)
			{
				Node node = endPoint(link);

				if (!items.TryGetValue(node.Name, out ReferenceItem item))
				{
					ReferenceItem parentItem = GetParentMenuItem(items, node.Parent);

					item = new ReferenceItem(node);
					items[node.Name] = item;

					parentItem.Items.Add(item);
				}

				item.Link = link;
			}

			ReferenceItem rootItem = items[NodeName.Root];
			List<ReferenceItem> compressedItems = CompressSubItems(rootItem).ToList();
			CompressNames(null, compressedItems);

			return compressedItems;
		}


		private void CompressNames(
			string parentName, List<ReferenceItem> itemsList)
		{
			foreach (ReferenceItem item in itemsList)
			{
				string fullName = item.Node.Name.DisplayFullNoParametersName;
				if (parentName != null && fullName.StartsWith(parentName) && fullName.Length > parentName.Length + 1)
				{
					item.Text = fullName.Substring(parentName.Length + 1);
				}
				else
				{
					item.Text = fullName;
				}

				CompressNames(fullName, item.Items);
			}
		}


		private static ReferenceItem GetParentMenuItem(
			IDictionary<NodeName, ReferenceItem> items, Node parent)
		{
			if (items.TryGetValue(parent.Name, out ReferenceItem parentItem))
			{
				return parentItem;
			}

			parentItem = new ReferenceItem(parent);

			if (!parent.IsRoot)
			{
				ReferenceItem grandParentItem = GetParentMenuItem(items, parent.Parent);
				grandParentItem.Items.Add(parentItem);
			}

			items[parent.Name] = parentItem;
			return parentItem;
		}


		private IEnumerable<ReferenceItem> CompressSubItems(ReferenceItem item)
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
						IEnumerable<ReferenceItem> subItems = CompressSubItems(subItem);

						if (subItem.Link != null)
						{
							// Item has a link so we need it and its compressed sub-items
							ReferenceItem newItem = new ReferenceItem(subItem.Node)
							{
								Link = subItem.Link

							};

							newItem.Items.AddRange(subItems);
							yield return newItem;
						}
						else
						{
							// Skip this item and use its compressed sub-items
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


		private IEnumerable<ReferenceItemViewModel> ToReferenceItemViewModels(
			IEnumerable<ReferenceItem> compressedItems)
		{
			return compressedItems
				.Select(item => new ReferenceItemViewModel(
					this, item.Link, item.Text, ToReferenceItemViewModels(item.Items)));
		}


		private class ReferenceItem
		{
			public ReferenceItem(Node node) => Node = node;

			public string Text { get; set; }

			public Node Node { get; }
			public Link Link { get; set; }
			public List<ReferenceItem> Items { get; } = new List<ReferenceItem>();
			public override string ToString() => $"{Node}";
		}


		//private IEnumerable<ReferenceItemViewModel> GetLinkItems(
		//	IEnumerable<Link> links, Func<IEdge, Node> endPoint, int level)
		//{
		//	return links.Select(link => new ReferenceItemViewModel(
		//		this, link, GetNodeText(endPoint(link)), EmptySubLinks));

		//	//if (!links.Any())
		//	//{
		//	//	return new List<ReferenceItemViewModel>();
		//	//}
		//	//else if (links.Count() < LinksMenuLimit)
		//	//{
		//	//	return links.Select(link => new ReferenceItemViewModel(
		//	//		this, link, GetNodeText(endPoint(link)), EmptySubLinks));
		//	//}

		//	//var groups = links
		//	//	.GroupBy(link => GetGroupKey(endPoint(link), level))
		//	//	.OrderBy(group => group.Key);

		//	//List<ReferenceItemViewModel> linkItems = GetLinksGroupsItems(groups, endPoint, level);

		//	////ReduceHierarchy(linkItems);

		//	//return linkItems.OrderBy(item => item, LinkItemComparer());
		//}


		//private static string GetNodeText(Node node) => node.Name.DisplayFullNoParametersName;

		//private static Comparer<ReferenceItemViewModel> LinkItemComparer()
		//{
		//	return Comparer<ReferenceItemViewModel>.Create(CompareLinkItems);
		//}

		//private static int CompareLinkItems(ReferenceItemViewModel i1, ReferenceItemViewModel i2)
		//	=> i1.Text.CompareTo(i2.Text);


		//private static int CompareLinkItems(ReferenceItemViewModel i1, ReferenceItemViewModel i2)
		//	=> i1.Text == MoreText && i2.Text == MoreText
		//		? 0
		//		: i1.Text == MoreText
		//			? 1
		//			: i2.Text == MoreText
		//				? -1
		//				: i1.Text.CompareTo(i2.Text);




		//private List<ReferenceItemViewModel> GetLinksGroupsItems(
		//	IEnumerable<IGrouping<string, Link>> linksGroups, Func<IEdge, Node> endPoint, int level)
		//{
		//	var linkItems = linksGroups
		//		.Take(LinksMenuLimit)
		//		.Select(group => GetLinkItem(group, endPoint, level));

		//	if (linksGroups.Count() > LinksMenuLimit)
		//	{
		//		List<ReferenceItemViewModel> moreLinks = GetLinksGroupsItems(linksGroups.Skip(LinksMenuLimit), endPoint, level);

		//		linkItems = linkItems.Concat(new[] { new ReferenceItemViewModel(this, null, MoreText, moreLinks) });
		//	}

		//	return linkItems.ToList();
		//}


		//private ReferenceItemViewModel GetLinkItem(
		//	IGrouping<string, Link> linksGroup, Func<IEdge, Node> endPoint, int level)
		//{
		//	IEnumerable<ReferenceItemViewModel> subLinks = GetLinkItems(linksGroup, endPoint, level + 1);

		//	ReferenceItemViewModel itemViewModel = new ReferenceItemViewModel(this, null, GetGroupText(linksGroup.Key), subLinks);

		//	return itemViewModel;
		//}


		//private static string GetGroupText(string key)
		//{
		//	string[] parts = key.Split(".".ToCharArray());
		//	string fullName = string.Join(".", parts
		//		.Where(part => !part.StartsWithTxt("$") && !part.StartsWithTxt("?")));

		//	if (string.IsNullOrEmpty(fullName))
		//	{
		//		fullName = key;
		//	}

		//	return NodeName.ToNiceText(fullName);
		//}





		//private static string GetGroupKey(Node node, int level)
		//{
		//	string[] parts = node.Name.FullName.Split(".".ToCharArray());
		//	return string.Join(".", parts.Take(level));
		//}



	}
}