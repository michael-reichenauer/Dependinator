using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineMenuItemService : ILineMenuItemService
	{
		private static readonly string MoreText = "...";
		private static readonly int LinksMenuLimit = 35;

		private static readonly IEnumerable<LineMenuItemViewModel> EmptySubLinks = Enumerable.Empty<LineMenuItemViewModel>();


		public IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(Line line)
		{
			return GetLineLinkItems(line, link => link.Source);
		}


		public IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(Line line)
		{
			return GetLineLinkItems(line, link => link.Target);
		}


		public IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(IEnumerable<Line> lines)
		{
			return GetLinkItems(lines, line => line.Source);
		}


		public IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(IEnumerable<Line> lines)
		{
			return GetLinkItems(lines, line => line.Target);
		}


		public IEnumerable<LineMenuItemViewModel> GetLinkItems(
			IEnumerable<Line> lines,
			Func<IEdge, Node> endPoint)
		{
			List<LineMenuItemViewModel> lineItems = new List<LineMenuItemViewModel>();

			foreach (Line line in lines)
			{
				IEnumerable<LineMenuItemViewModel> linkItems = GetLineLinkItems(line, endPoint);

				if (line.IsToChild)
				{
					lineItems.AddRange(linkItems);
				}
				else
				{
					// Line to parent or sibling
					string nodeName = endPoint(line).Name.DisplayName;
					LineMenuItemViewModel lineMenuItemViewModel = new LineMenuItemViewModel(null, nodeName, linkItems);

					lineItems.Add(lineMenuItemViewModel);
				}
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


		private IEnumerable<LineMenuItemViewModel> GetLineLinkItems(
			Line line, Func<IEdge, Node> endPoint)
		{
			IEnumerable<Link> lineLinks = line.Links.DistinctBy(link => endPoint(link));

			return GetLinkItems(lineLinks, endPoint, 1);
		}


		private IEnumerable<LineMenuItemViewModel> GetLinkItems(
			IEnumerable<Link> links, Func<IEdge, Node> endPoint, int level)
		{
			if (!links.Any())
			{
				return new List<LineMenuItemViewModel>();
			}
			else if (links.Count() < LinksMenuLimit)
			{
				return links.Select(link => new LineMenuItemViewModel(
					link, GetLinkText(endPoint(link)), EmptySubLinks));
			}

			var groups = links
				.GroupBy(link => GetGroupKey(endPoint(link), level))
				.OrderBy(group => group.Key);

			List<LineMenuItemViewModel> linkItems = GetLinksGroupsItems(groups, endPoint, level);

			//ReduceHierarchy(linkItems);

			return linkItems.OrderBy(item => item, LinkItemComparer());
		}


		private static Comparer<LineMenuItemViewModel> LinkItemComparer()
		{
			return Comparer<LineMenuItemViewModel>.Create(CompareLinkItems);
		}


		private static int CompareLinkItems(LineMenuItemViewModel i1, LineMenuItemViewModel i2)
			=> i1.Text == MoreText && i2.Text == MoreText
				? 0
				: i1.Text == MoreText
					? 1
					: i2.Text == MoreText
						? -1
						: i1.Text.CompareTo(i2.Text);


		private static void ReduceHierarchy(ICollection<LineMenuItemViewModel> linkItems)
		{
			int margin = Math.Max(LinksMenuLimit - linkItems.Count, 0);

			while (margin >= 0)
			{
				bool isChanged = false;

				foreach (LineMenuItemViewModel linkItem in linkItems.Where(item => item.SubLinkItems.Any()).ToList())
				{
					int count = linkItem.SubLinkItems.Count();

					if (count > 4)
					{
						continue;
					}

					margin -= (count - 1);

					if (margin >= 0)
					{
						linkItems.Remove(linkItem);
						linkItem.SubLinkItems.ForEach(linkItems.Add);
						isChanged = true;
					}
				}

				if (!isChanged)
				{
					break;
				}
			}
		}


		private List<LineMenuItemViewModel> GetLinksGroupsItems(
			IEnumerable<IGrouping<string, Link>> linksGroups, Func<IEdge, Node> endPoint, int level)
		{
			var linkItems = linksGroups
				.Take(LinksMenuLimit)
				.Select(group => GetLinkItem(group, endPoint, level));

			if (linksGroups.Count() > LinksMenuLimit)
			{
				List<LineMenuItemViewModel> moreLinks = GetLinksGroupsItems(linksGroups.Skip(LinksMenuLimit), endPoint, level);

				linkItems = linkItems.Concat(new[] { new LineMenuItemViewModel(null, MoreText, moreLinks) });
			}

			return linkItems.ToList();
		}


		private LineMenuItemViewModel GetLinkItem(
			IGrouping<string, Link> linksGroup, Func<IEdge, Node> endPoint, int level)
		{
			IEnumerable<LineMenuItemViewModel> subLinks = GetLinkItems(linksGroup, endPoint, level + 1);

			LineMenuItemViewModel itemViewModel = new LineMenuItemViewModel(null, GetGroupText(linksGroup.Key), subLinks);

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