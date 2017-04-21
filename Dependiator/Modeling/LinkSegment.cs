using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal class LinkSegment
	{
		private static readonly char[] Separator = ".".ToCharArray();

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

		public bool CanBeShown()
		{
			return (Source.CanShowNode() && Target.CanShowNode());
		}


		public string GetToolTip()
		{
			string source = Source.NodeName.ToString();
			string[] sourceParts = source.Split(Separator);
			string[] targetParts = Target.NodeName.ToString().Split(Separator);

			string tip = $"";

			int sourcePartsCount = 0;
			int targetPartsCount = 0;
			int count = 0;

			if (Source.ParentNode == Target.ParentNode)
			{
				// Siblings
				sourcePartsCount = sourceParts.Length + 1;
				targetPartsCount = targetParts.Length + 1;

			}
			else if (Source == Target.ParentNode)
			{
				// Source is parent of target
				sourcePartsCount = sourceParts.Length;
				targetPartsCount = targetParts.Length + 1;
			}
			else if (Source.ParentNode == Target)
			{
				// Source is child of target
				sourcePartsCount = sourceParts.Length + 1;
				targetPartsCount = targetParts.Length;
			}

			var groupBySources = NodeLinks.GroupBy(l => GetName(l.Source.NodeName, sourcePartsCount));

			foreach (var group in groupBySources)
			{
				string groupName = group.Key;

				var groupedTargets = group.GroupBy(l => GetName(l.Target.NodeName, targetPartsCount));

				tip += $"\n  {groupName} ({group.Count()}):";
				foreach (var reference in groupedTargets)
				{
					tip += $"\n     -> {reference.Key} ({reference.Count()})";
					count++;
				}
			}

			tip = $"{NodeLinks.Count} links, splits into {count} links:"  + tip;
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


		string GetName(string fullname, int parts)
		{
			return string.Join(".", fullname.Split(Separator).Take(parts));
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


		public override string ToString() => $"{Source} -> {Target}";
	}
}