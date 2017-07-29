using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Links
{
	internal class LinkLineOld : Equatable<LinkLineOld>
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly List<LinkOld> links = new List<LinkOld>();
		private readonly List<LinkOld> hiddenLinks = new List<LinkOld>();


		private Rect itemBounds;


		private bool isUpdated = false;


		public LinkLineOld(
			ILineViewModelService lineViewModelService,
			NodeOld source,
			NodeOld target,
			NodeOld owner)
		{
			this.lineViewModelService = lineViewModelService;
			Owner = owner;
			Source = source;
			Target = target;

			ViewModel = new LinkLineViewModel(lineViewModelService, this);
			IsEqualWhenSame(Source, Target);
		}

		public bool IsNormal { get; set; }

		public bool IsEmpty => !links.Any();
		public bool HasHidden => hiddenLinks.Any();
		public bool IsMouseOver => ViewModel.IsMouseOver;

		public LinkLineViewModel ViewModel { get; }


		public Point L1 { get; private set; }

		public Point L2 { get; private set; }


		public double ItemsScale => Owner.ItemsScale;

		public IReadOnlyList<LinkOld> Links => links;
		public IReadOnlyList<LinkOld> HiddenLinks => hiddenLinks;


		public NodeOld Source { get; }

		public NodeOld Target { get; }

		public NodeOld Owner { get; }


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
					IEnumerable<LinkLineOld> linkLines = Source.Links.ReferencingLines
						.Where(l => l.Source == Source)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.ToList();

					foreach (LinkLineOld linkLine in linkLines)
					{
						if (!linkLine.IsEmpty)
						{
							lineViewModelService.ZoomInLinkLine(linkLine, Source);
							break;
						}
					}
				}
				else
				{
					IEnumerable<LinkLineOld> linkLines = Source.Links.ReferencingLines
						.Where(l => l.Source == Source)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.Reverse()
						.ToList();

					foreach (LinkLineOld linkLine in linkLines)
					{
						if (!linkLine.IsNormal && !linkLine.IsEmpty)
						{
							lineViewModelService.ZoomOutLinkLine(linkLine, Source);
							break;
						}
					}
				}
			}
			else
			{
				if (zoom > 1)
				{
					IEnumerable<LinkLineOld> linkLines = Target.Links.ReferencingLines
						.Where(l => l.Target == Target)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.ToList();

					foreach (LinkLineOld linkLine in linkLines)
					{
						if (!linkLine.IsEmpty)
						{
							lineViewModelService.ZoomInLinkLine(linkLine, Target);
							break;
						}
					}
				}
				else
				{
					IEnumerable<LinkLineOld> linkLines = Target.Links.ReferencingLines
						.Where(l => l.Target == Target)
						.Where(l => l.links.Intersect(links.Concat(hiddenLinks)).Any())
						.Reverse()
						.ToList();

					foreach (LinkLineOld linkLine in linkLines)
					{
						if (!linkLine.IsNormal && !linkLine.IsEmpty)
						{
							lineViewModelService.ZoomOutLinkLine(linkLine, Target);
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


		public bool TryAddLink(LinkOld link)
		{
			hiddenLinks.Remove(link);
			return links.TryAdd(link);
		}

		public void AddLink(LinkOld link)
		{
			//hiddenLinks.Remove(link);
			links.Add(link);
		}



		public void HideLink(LinkOld link)
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
				//ViewModel.Show();
				ViewModel.NotifyAll();
			}
			else
			{
				if (ViewModel.CanShow)
				{
					//ViewModel.Hide();
					ViewModel.NotifyAll();
				}
			}

			Owner.UpdateItem(ViewModel);
		}


		public void ToggleLine()
		{
			if (!IsNormal && IsEmpty)
			{
				lineViewModelService.CloseLine(this);
				UpdateVisibility();
			}
		}




		private void UpdateSegmentLine()
		{
			LinkLineBounds lineBounds = lineViewModelService.GetLinkLineBounds(this);

			itemBounds = lineBounds.ItemBounds;
			L1 = lineBounds.Source;
			L2 = lineBounds.Target;
		}


		public override string ToString() => $"{Source} -> {Target} ({links.Count})";
	}
}