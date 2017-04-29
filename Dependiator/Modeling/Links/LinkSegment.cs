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
		private readonly ILinkService linkService;
		private readonly List<Link> nodeLinks = new List<Link>();
		private Rect itemBounds;


		private bool isUpdated = false;


		public LinkSegment(
			ILinkService linkService,
			Node source,
			Node target,
			Node owner)
		{
			this.linkService = linkService;
			Owner = owner;
			Source = source;
			Target = target;

			ViewModel = new LinkSegmentViewModel(linkService, this);
		}


		public bool IsEmpty { get; private set; }


		public LinkSegmentViewModel ViewModel { get; }


		public Point L1 { get; private set; }

		public Point L2 { get; private set; }


		public double ItemsScale => Owner.ItemsScale;

		public IReadOnlyList<Link> NodeLinks => nodeLinks;

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }

	
		public bool CanShowSegment() => Source.CanShowNode() || Target.CanShowNode();


		public Rect GetItemBounds()
		{
			if (!isUpdated)
			{
				isUpdated = true;
				
				LinkSegmentLine line = linkService.GetLinkSegmentLine(this);
				UpdateBounds(line);
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
			IReadOnlyList<LinkGroup> linkGroups = linkService.GetLinkGroups(this);

			foreach (LinkGroup group in linkGroups)
			{
				Node commonAncestor = group.Source.Ancestors()
					.First(node => group.Target.Ancestors().Contains(node));

				LinkSegment segment = new LinkSegment(
					linkService, group.Source, group.Target, commonAncestor);
				group.Links.ForEach(link => segment.Add(link));
				
				commonAncestor.AddOwnedSegment(segment);
			}

			IsEmpty = true;
			UpdateVisibility();
		}


		private void UpdateBounds(LinkSegmentLine line)
		{
			itemBounds = line.ItemBounds;
			L1 = line.Source;
			L2 = line.Target;
		}


		public override string ToString() => $"{Source} -> {Target}";

		protected override bool IsEqual(LinkSegment other)
			=> Source == other.Source && Target == other.Target;

		protected override int GetHash() => GetHashes(Source, Target);	
	}
}