using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal class LinkSegment
	{
		private readonly IItemService itemService;
		private readonly List<Link> nodeLinks = new List<Link>();
		private Rect itemBounds;

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

		public Brush LinkBrush => Source == Target.ParentNode
			? Target.GetNodeBrush()
			: Source.GetNodeBrush();


		public double LineThickness => GetLineThickness();

		public bool CanBeShown() => (Source.CanShowNode() && Target.CanShowNode());

		public string GetToolTip()
		{
			string tip = $"{this},  {NodeLinks.Count} references:";
			int maxLinks = 40;

			foreach (Link reference in NodeLinks.Take(maxLinks))
			{
				tip += $"\n  {reference}";
			}

			if (NodeLinks.Count > maxLinks)
			{
				tip += "\n  ...";
			}

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
			if (nodeLinks.Any(l => l.Source == link.Source && l.Target == link.Target))
			{
				return;
			}

			nodeLinks.Add(link);
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


		public override string ToString() => $"{Source} -> {Target}";
	}
}