using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Nodes;
using Dependiator.Utils;


namespace Dependiator.Modeling.Links
{
	internal class LinkSegment : Equatable<LinkSegment>
	{
		private readonly IItemService itemService;
		private readonly List<Link> nodeLinks = new List<Link>();
		private Rect itemBounds;

		private bool isEmpty = false;

		private bool isUpdated = false;

		public LinkSegment(
			IItemService itemService,
			Node source,
			Node target,
			Node owner)
		{
			this.itemService = itemService;
			Owner = owner;
			Source = source;
			Target = target;

			ViewModel = new LinkSegmentViewModel(this);
		}


		public LinkSegmentViewModel ViewModel { get; }


		public Point L1 { get; private set; }

		public Point L2 { get; private set; }



		public double LinkScale => Owner.ItemsScale;

		public IReadOnlyList<Link> NodeLinks => nodeLinks;

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }

		public Brush LinkBrush => isEmpty ? Brushes.DimGray :
			Source == Target.ParentNode
			? Target.GetNodeBrush()
			: Source.GetNodeBrush();


		public double LineThickness => GetLineThickness();

		public bool CanBeShown()
		{
			return (Source.CanShowNode() && Target.CanShowNode());
		}


		public string GetToolTip()
		{
			IReadOnlyList<LinkGroup> linkGroups = GetLinkGroups();
			string tip = "";
		
			foreach (var group in linkGroups)
			{
				tip += $"\n  {group.Source} -> {group.Target} ({group.Links.Count})";
			}

			tip = $"{this} {NodeLinks.Count} links, splits into {linkGroups.Count} links:" + tip;

			//int maxLinks = 40;
			//tip += $"\n";

			//foreach (Link reference in NodeLinks.Take(maxLinks))
			//{
			//	tip += $"\n  {reference}";
			//}

			//if (NodeLinks.Count > maxLinks)
			//{
			//	tip += "\n  ...";
			//}


			return tip;
		}



		public Rect GetItemBounds()
		{
			if (!isUpdated)
			{
				isUpdated = true;

				if (Source.NodeBounds == Rect.Empty || Target.NodeBounds == Rect.Empty)
				{
					return Rect.Empty;
				}

				itemService.UpdateLine(this);

			}

			return itemBounds;
		}


		public void Add(Link link)
		{
			if (!nodeLinks.Contains(link))
			{
				nodeLinks.Add(link);
			}
		}



		public void UpdateVisibility()
		{
			isUpdated = false;
			if (CanBeShown())
			{
				ViewModel.Show();
				ViewModel.NotifyAll();
			}
			else
			{
				if (ViewModel.CanShow)
				{
					ViewModel.Hide();
					ViewModel.NotifyAll();
				}
			}

			Owner.UpdateItem(ViewModel);
		}


		private double GetLineThickness()
		{
			double scale = (Owner.ItemsScale).MM(0.1, 0.7);
			double thickness;

			if (NodeLinks.Count < 5)
			{
				thickness = 1;
			}
			else if (NodeLinks.Count < 15)
			{
				thickness = 2;
			}
			else
			{
				thickness = 3;
			}

			return thickness * scale;
		}


		public void SetBounds(Rect lineBounds, Point l1, Point l2)
		{
			itemBounds = lineBounds;
			L1 = l1;
			L2 = l2;
		}


		public void ToggleLine()
		{
			IReadOnlyList<LinkGroup> linkGroups = GetLinkGroups();

			foreach (LinkGroup group in linkGroups)
			{
				Node commonAncestor = group.Source.Ancestors()
					.First(node => group.Target.Ancestors().Contains(node));

				LinkSegment segment = new LinkSegment(
					itemService, group.Source, group.Target, commonAncestor);
				group.Links.ForEach(link => segment.Add(link));
				
				commonAncestor.AddOwnedSegment(segment);
			}
			ViewModel.StrokeDash = "2,2";
			isEmpty = true;
			UpdateVisibility();
		}


		public override string ToString() => $"{Source} -> {Target}";

		protected override bool IsEqual(LinkSegment other)
			=> Source == other.Source && Target == other.Target;


		protected override int GetHash() => GetHashes(Source, Target);


		private IReadOnlyList<LinkGroup> GetLinkGroups()
		{
			int sourceLevel = Source.Ancestors().Count();
			int targetLevel = Target.Ancestors().Count();

			if (Source == Target.ParentNode)
			{
				// Source is parent of target
				targetLevel += 1;
			}
			else if (Source.ParentNode == Target)
			{
				// Source is child of target
				sourceLevel += 1;
			}
			else
			{
				// Siblings
				sourceLevel += 1;
				targetLevel += 1;
			}

			var groupBySources = NodeLinks.GroupBy(l => NodeAtLevel(l.Source, sourceLevel));

			List<LinkGroup> linkGroups = new List<LinkGroup>();
			foreach (var sourceGroup in groupBySources)
			{
				var groupByTargets = sourceGroup.GroupBy(l => NodeAtLevel(l.Target, targetLevel));

				foreach (var targetGroup in groupByTargets)
				{
					linkGroups.Add(new LinkGroup(sourceGroup.Key, targetGroup.Key, targetGroup.ToList()));
				}
			}

			return linkGroups;
		}


		private static Node NodeAtLevel(Node node, int sourceLevel)
		{
			int count = 0;
			Node current = null;
			foreach (Node ancestor in node.AncestorsAndSelf().Reverse())
			{
				current = ancestor;
				if (count++ == sourceLevel)
				{
					break;
				}
			}

			return current;
		}


		private class LinkGroup
		{
			public LinkGroup(Node source, Node target, IReadOnlyList<Link> links)
			{
				Source = source;
				Target = target;
				Links = links;
			}


			public Node Source { get; }
			public Node Target { get; }
			public IReadOnlyList<Link> Links { get; }

			public override string ToString() => $"{Source} -> {Target} ({Links.Count})";
		}

	}
}