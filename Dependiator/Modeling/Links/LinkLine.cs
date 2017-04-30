using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Nodes;
using Dependiator.Utils;


namespace Dependiator.Modeling.Links
{
	internal class LinkLine : Equatable<LinkLine>
	{
		private readonly ILinkService linkService;
		private readonly List<Link> links = new List<Link>();
		private readonly List<Node> referencingNodes = new List<Node>();

		private Rect itemBounds;


		private bool isUpdated = false;


		public LinkLine(
			ILinkService linkService,
			Node source,
			Node target,
			Node owner)
		{
			this.linkService = linkService;
			Owner = owner;
			Source = source;
			Target = target;

			ViewModel = new LinkLineViewModel(linkService, this);
		}


		public bool IsEmpty { get; private set; }


		public LinkLineViewModel ViewModel { get; }


		public Point L1 { get; private set; }

		public Point L2 { get; private set; }


		public double ItemsScale => Owner.ItemsScale;

		public IReadOnlyList<Link> Links => links;

		public IReadOnlyList<Node> ReferencingNodes => referencingNodes;

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }


		public bool CanShowSegment()
		{
			if (Source == Target.ParentNode
			    || Source.ParentNode == Target
			    || Source.ParentNode == Target.ParentNode)
			{
				return Source.CanShowNode() && Target.CanShowNode();
			}
			else
			{
				return Source.CanShowNode() || Target.CanShowNode();
			}
		}
		


		public Rect GetItemBounds()
		{
			if (!isUpdated)
			{							
				UpdateSegmentLine();
				isUpdated = true;
			}

			return itemBounds;
		}


		public bool TryAddLink(Link link) => links.TryAdd(link);

		public bool TryAddReferencingNode(Node node) => referencingNodes.TryAdd(node);


		public void UpdateVisibility()
		{
			isUpdated = false;
			if (CanShowSegment())
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


		public void ToggleLine()
		{
			if (!IsEmpty)
			{
				IReadOnlyList<LinkGroup> linkGroups = linkService.GetLinkGroups(this);

				foreach (LinkGroup group in linkGroups)
				{
					Node commonAncestor = group.Source.Ancestors()
						.First(node => group.Target.Ancestors().Contains(node));

					LinkLine line = new LinkLine(
						linkService, group.Source, group.Target, commonAncestor);
					group.Links.ForEach(link => line.TryAddLink(link));

					commonAncestor.AddOwnedLine(line);
				}

				IsEmpty = true;
			}

			UpdateVisibility();
		}


		private void UpdateSegmentLine()
		{
			LinkLineBounds lineBounds = linkService.GetLinkLineBounds(this);

			itemBounds = lineBounds.ItemBounds;
			L1 = lineBounds.Source;
			L2 = lineBounds.Target;
		}


		public override string ToString() => $"{Source} -> {Target}";

		protected override bool IsEqual(LinkLine other)
			=> Source == other.Source && Target == other.Target;

		protected override int GetHash() => GetHashes(Source, Target);	
	}
}