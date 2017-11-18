using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links.Private
{
	internal class LinkMenuItemService : ILinkMenuItemService
	{
		private static readonly string MoreText = "...";
		private static readonly int LinksMenuLimit = 35;

		private static readonly IEnumerable<LinkItem> EmptySubLinks = Enumerable.Empty<LinkItem>();


		public IEnumerable<LinkItem> GetTargetLinkItems(IEnumerable<Line> lines)
		{
			IEnumerable<LinkItem> items = GetLinkItems(
				lines, line => line.Source, link => link.Source);

			return items;
		}


		public IEnumerable<LinkItem> GetSourceLinkItems(IEnumerable<Line> lines)
		{
			IEnumerable<LinkItem> items = GetLinkItems(
				lines, line => line.Target, link => link.Target);
			return items;
		}



		private IEnumerable<LinkItem> GetLinkItems(
			IEnumerable<Line> lines, Func<Line, Node> lineEndPoint, Func<Link, Node> linkEndPoint)
		{
			List<LinkItem> lineItems = new List<LinkItem>();
			foreach (Line line in lines)
			{
				IEnumerable<Link> lineLinks = line.Links.DistinctBy(linkEndPoint);
				IEnumerable<LinkItem> linkItems = GetLinkItems(lineLinks, linkEndPoint, 1);
				lineItems.Add(new LinkItem(null, lineEndPoint(line).Name.DisplayName, linkItems));
			}

			return lineItems;
		}


		private IEnumerable<LinkItem> GetLinkItems(
			IEnumerable<Link> links, Func<Link, Node> endPoint, int level)
		{
			if (!links.Any())
			{
				return new List<LinkItem>();
			}
			else if (links.Count() < LinksMenuLimit)
			{
				return links.Select(link => new LinkItem(
					link, endPoint(link).Name.DisplayFullName, EmptySubLinks));
			}

			var groups = links
				.GroupBy(link => GetGroupKey(endPoint(link), level))
				.OrderBy(group => group.Key);

			List<LinkItem> linkItems = GetLinksGroupsItems(groups, endPoint, level);

			ReduceHierarchy(linkItems);

			linkItems.Sort(Comparer<LinkItem>.Create(
				(i1, i2) => i1.Text == MoreText
					? 1 : i2.Text == MoreText ? -1 : i1.Text.CompareTo(i2.Text)));

			return linkItems;
		}


		private static void ReduceHierarchy(ICollection<LinkItem> linkItems)
		{
			int margin = Math.Max(LinksMenuLimit - linkItems.Count, 0);

			while (margin >= 0)
			{
				bool isChanged = false;

				foreach (LinkItem linkItem in linkItems.Where(item => item.SubLinkItems.Any()).ToList())
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


		private List<LinkItem> GetLinksGroupsItems(
			IEnumerable<IGrouping<string, Link>> linksGroups, Func<Link, Node> endPoint, int level)
		{
			var linkItems = linksGroups
				.Take(LinksMenuLimit)
				.Select(group => GetLinkItem(group, endPoint, level));

			if (linksGroups.Count() > LinksMenuLimit)
			{
				List<LinkItem> moreLinks = GetLinksGroupsItems(linksGroups.Skip(LinksMenuLimit), endPoint, level);

				linkItems = linkItems.Concat(new[] { new LinkItem(null, MoreText, moreLinks) });
			}

			return linkItems.ToList();
		}


		private LinkItem GetLinkItem(
			IGrouping<string, Link> linksGroup, Func<Link, Node> endPoint, int level)
		{
			IEnumerable<LinkItem> subLinks = GetLinkItems(linksGroup, endPoint, level + 1);

			LinkItem item = new LinkItem(null, GetGroupText(linksGroup.Key), subLinks);

			return item;
		}


		private static string GetGroupText(string key)
		{
			return key.Replace("$", "").Replace("?", "").Replace("*", ".");
		}


		string GetGroupKey(Node node, int level)
		{
			string[] parts = node.Name.FullName.Split(".".ToCharArray());
			return string.Join(".", parts.Take(level));
		}
	}
}