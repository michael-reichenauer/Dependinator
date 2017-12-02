using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links.Private
{
	internal class LinkMenuItemService : ILinkMenuItemService
	{
		private static readonly string MoreText = "...";
		private static readonly int LinksMenuLimit = 35;

		private static readonly IEnumerable<LinkItem> EmptySubLinks = Enumerable.Empty<LinkItem>();


		public IEnumerable<LinkItem> GetSourceLinkItems(Line line)
		{
			return GetLineLinkItems(line, link => link.Source);
		}


		public IEnumerable<LinkItem> GetTargetLinkItems(Line line)
		{
			return GetLineLinkItems(line, link => link.Target);
		}


		public IEnumerable<LinkItem> GetSourceLinkItems(IEnumerable<Line> lines)
			=> GetLinesLinkItems(lines, line => line.Source, link => link.Source);


		public IEnumerable<LinkItem> GetTargetLinkItems(IEnumerable<Line> lines)
			=> GetLinesLinkItems(lines, line => line.Target, link => link.Target);


		private IEnumerable<LinkItem> GetLinesLinkItems(
			IEnumerable<Line> lines, Func<Line, Node> lineEndPoint, Func<Link, Node> linkEndPoint)
		{
			List<LinkItem> lineItems = new List<LinkItem>();
			foreach (Line line in lines)
			{
				IEnumerable<LinkItem> linkItems = GetLineLinkItems(line, linkEndPoint);
				LinkItem linkItem = new LinkItem(null, lineEndPoint(line).Name.DisplayName, linkItems);

				lineItems.Add(linkItem);
			}

			return lineItems;
		}


		private IEnumerable<LinkItem> GetLineLinkItems(Line line, Func<Link, Node> linkEndPoint)
		{
			IEnumerable<Link> lineLinks = line.Links.DistinctBy(linkEndPoint);
			return GetLinkItems(lineLinks, linkEndPoint, 1);
		}


		private IEnumerable<LinkItem> GetLinkItems(
			IEnumerable<Link> links, Func<Link, Node> linkEndPoint, int level)
		{
			if (!links.Any())
			{
				return new List<LinkItem>();
			}
			else if (links.Count() < LinksMenuLimit)
			{
				return links.Select(link => new LinkItem(
					link, GetLinkText(linkEndPoint(link)), EmptySubLinks));
			}

			var groups = links
				.GroupBy(link => GetGroupKey(linkEndPoint(link), level))
				.OrderBy(group => group.Key);

			List<LinkItem> linkItems = GetLinksGroupsItems(groups, linkEndPoint, level);

			ReduceHierarchy(linkItems);

			return linkItems.OrderBy(item => item, LinkItemComparer());
		}


		private static Comparer<LinkItem> LinkItemComparer()
		{
			return Comparer<LinkItem>.Create(CompareLinkItems);
		}


		private static int CompareLinkItems(LinkItem i1, LinkItem i2)
			=> i1.Text == MoreText && i2.Text == MoreText
				? 0
				: i1.Text == MoreText
					? 1
					: i2.Text == MoreText
						? -1
						: i1.Text.CompareTo(i2.Text);


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