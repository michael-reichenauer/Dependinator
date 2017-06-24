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
		private readonly List<Link> hiddenLinks = new List<Link>();
		

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
			IsEqualWhen(other => Source == other.Source && Target == other.Target, Source, Target);
		}

		public bool IsNormal { get; set; }

		public bool IsEmpty => !links.Any();
		public bool HasHidden => hiddenLinks.Any();
		public bool IsMouseOver => ViewModel.IsMouseOver;

		public LinkLineViewModel ViewModel { get; }


		public Point L1 { get; private set; }

		public Point L2 { get; private set; }


		public double ItemsScale => Owner.ItemsScale;

		public IReadOnlyList<Link> Links => links;
		public IReadOnlyList<Link> HiddenLinks => hiddenLinks;


		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }


		public bool CanShowSegment() => 
			!IsNormal 
			|| (Source.CanShowNode() && Target.CanShowNode());



		public void ZoomLinks(double zoom, Point viewPosition)
		{
			bool isStart = (viewPosition - L1).Length < (viewPosition - L2).Length;

			if (isStart)
			{
				if (zoom > 1)
				{
					IEnumerable<LinkLine> linkLines = Source.Links.ReferencingLines
						.Where(l => l.Source == Source)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.ToList();

					foreach (LinkLine linkLine in linkLines)
					{
						if (!linkLine.IsEmpty)
						{
							linkService.ZoomInLinkLine(linkLine, Source);
							break;
						}
					}
				}
				else
				{
					IEnumerable<LinkLine> linkLines = Source.Links.ReferencingLines
						.Where(l => l.Source == Source)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.Reverse()
						.ToList();

					foreach (LinkLine linkLine in linkLines)
					{
						if (!linkLine.IsNormal && !linkLine.IsEmpty)
						{
							linkService.ZoomOutLinkLine(linkLine, Source);
							break;
						}
					}
				}
			}
			else
			{
				if (zoom > 1)
				{
					IEnumerable<LinkLine> linkLines = Target.Links.ReferencingLines
						.Where(l => l.Target == Target)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.ToList();

					foreach (LinkLine linkLine in linkLines)
					{
						if (!linkLine.IsEmpty)
						{
							linkService.ZoomInLinkLine(linkLine, Target);
							break;
						}
					}
				}
				else
				{
					IEnumerable<LinkLine> linkLines = Target.Links.ReferencingLines
						.Where(l => l.Target == Target)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.Reverse()
						.ToList();

					foreach (LinkLine linkLine in linkLines)
					{
						if (!linkLine.IsNormal && !linkLine.IsEmpty)
						{
							linkService.ZoomOutLinkLine(linkLine, Target);
							break;
						}
					}
				}
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


		public bool TryAddLink(Link link)
		{
			hiddenLinks.Remove(link);
			return links.TryAdd(link);
		}

		public void AddLink(Link link)
		{
			//hiddenLinks.Remove(link);
			links.Add(link);
		}



		public void HideLink(Link link)
		{
			if (links.Remove(link))
			{
				hiddenLinks.TryAdd(link);
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
			if (!IsNormal && IsEmpty)
			{
				linkService.CloseLine(this);
				UpdateVisibility();
			}	
		}




		private void UpdateSegmentLine()
		{
			LinkLineBounds lineBounds = linkService.GetLinkLineBounds(this);

			itemBounds = lineBounds.ItemBounds;
			L1 = lineBounds.Source;
			L2 = lineBounds.Target;
		}


		public override string ToString() => $"{Source} -> {Target} ({links.Count})";
	}
}