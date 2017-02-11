using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Link : Item
	{
		private readonly IItemService itemService;
		private readonly LinkViewModel linkViewModel;
		private Point sourcePoint;
		private Point targetPoint;


		public Link(
			IItemService itemService,
			Reference reference,
			Node owner,
			Node sourceNode,
			Node targetNode)
			: base(itemService, owner)
		{
			Reference = reference;
			this.itemService = itemService;
			this.SourceNode = sourceNode;
			this.TargetNode = targetNode;

			SetLinkLine();

			linkViewModel = new LinkViewModel(this);
			ViewModel = linkViewModel;
		}

		public Node SourceNode { get; }

		public Node TargetNode { get; }

		public Reference Reference { get; }


		public override ViewModel ViewModel { get; }

		public string ToolTip
		{
			get
			{
				string tip = $"{Reference},  {Reference.SubReferences.Count} references:";
				int maxLinks = 40;
				foreach (Reference reference in Reference.SubReferences.Take(maxLinks))
				{
					tip += $"\n  {reference.SubReferences[0]}";
				}

				if (Reference.SubReferences.Count > maxLinks)
				{
					tip += "\n  ...";
				}

				return tip;
			}
		}


		public Brush LinkBrush { get; private set; }

		public double X1 => sourcePoint.X;
		public double Y1 => sourcePoint.Y;
		public double X2 => targetPoint.X;
		public double Y2 => targetPoint.Y;

		public int SubLinkCount => Reference.SubReferences.Count; 

		public override bool CanBeShown()
		{
			return 
				SourceNode.CanBeShown() && TargetNode.CanBeShown()
				&& ParentItem.ItemScale > 3.5;
		}


		public override void ItemRealized()
		{
			base.ItemRealized();
		}


		public override void ChangedScale()
		{
			base.ChangedScale();
		}


		public override void ItemVirtualized()
		{
			if (IsRealized)
			{
				base.ItemVirtualized();
			}
		}


		public void SetLinkLine()
		{
			double x1;
			double y1;
			double x2;
			double y2;
			double scaleFactor = ParentItem?.ThisItemScaleFactor ?? ThisItemScaleFactor;
			//double scaleFactor2 = ParentItem?.ParentItem?.ThisItemScaleFactor ?? ThisItemScaleFactor;

			if (SourceNode == TargetNode.ParentItem)
			{
				Rect targetRect = TargetNode.ItemBounds;
				targetRect.Scale(1 / scaleFactor, 1 / scaleFactor);

				Rect sourceRect = SourceNode.ItemBounds;

				////sourceRect.Scale(1 / scaleFactor2, 1 / scaleFactor2);

				x1 = (sourceRect.Width / 2) * (scaleFactor / ThisItemScaleFactor);
				y1 = 0;
				x2 = targetRect.X + targetRect.Width / 2;
				y2 = targetRect.Y;
				LinkBrush = TargetNode.RectangleBrush;
			}
			else if (SourceNode.ParentItem == TargetNode.ParentItem)
			{
				Rect targetRect = TargetNode.ItemBounds;
				targetRect.Scale(1 / scaleFactor, 1 / scaleFactor);

				Rect sourceRect = SourceNode.ItemBounds;
				sourceRect.Scale(1 / scaleFactor, 1 / scaleFactor);

				x1 = sourceRect.X + sourceRect.Width / 2;
				y1 = sourceRect.Y + sourceRect.Height;
				x2 = targetRect.X + targetRect.Width / 2;
				y2 = targetRect.Y;
				LinkBrush = SourceNode.RectangleBrush;
			}
			else if (SourceNode.ParentItem == TargetNode)
			{
				Rect targetRect = TargetNode.ItemBounds;
				//targetRect.Scale(1 / ThisItemScaleFactor, 1 / ThisItemScaleFactor);

				Rect sourceRect = SourceNode.ItemBounds;
				sourceRect.Scale(1 / scaleFactor, 1 / scaleFactor);

				x1 = sourceRect.X + sourceRect.Width / 2;
				y1 = sourceRect.Y + sourceRect.Height;
				x2 = (targetRect.Width / 2) * ( scaleFactor / ThisItemScaleFactor);
				y2 = targetRect.Height * (scaleFactor / ThisItemScaleFactor);
				LinkBrush = SourceNode.RectangleBrush;
			}
			else
			{
				// Only child, sibling or parent nodes supported for now, no direct links
				return;
			}

			double x = Math.Min(x1, x2);
			double y = Math.Min(y1, y2);
			double width = Math.Abs(x2 - x1);
			double height = Math.Abs(y2 - y1);

		
			if (x1 <= x2 && y1 <= y2)
			{
				sourcePoint = new Point(0, 0);
				targetPoint = new Point(width, height);
			}
			else if (x1 <= x2 && y1 > y2)
			{
				sourcePoint = new Point(0, height);
				targetPoint = new Point(width, 0);
			}
			else if (x1 > x2 && y1 <= y2)
			{
				sourcePoint = new Point(width, 0);
				targetPoint = new Point(0, height);
			}
			else
			{
				sourcePoint = new Point(width, height);
				targetPoint = new Point(0, 0);
			}

			Rect bounds = new Rect(new Point(x, y), new Size(width + 1, height + 1));

			bounds.Scale(scaleFactor, scaleFactor);
			ItemBounds = bounds;
		}


		public override string ToString() => Reference.ToString();
	}
}