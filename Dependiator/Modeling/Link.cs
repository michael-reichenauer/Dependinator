using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Link : Node
	{
		private readonly INodeService nodeService;
		private Point sourcePoint;
		private Point targetPoint;
		private LinkViewModel linkViewModel;

		public Link(
			INodeService nodeService,
			Reference reference,
			Module owner,
			Module sourceNode,
			Module targetNode)
			: base(nodeService, owner)
		{
			Reference = reference;
			this.nodeService = nodeService;
			this.SourceNode = sourceNode;
			this.TargetNode = targetNode;

			SetLinkLine();

			linkViewModel = new LinkViewModel(this);
			ViewModel = linkViewModel;
		}

		public Module SourceNode { get; }

		public Module TargetNode { get; }

		public Reference Reference { get; }


		public override ViewModel ViewModel { get; }

		public string ToolTip
		{
			get
			{
				string tip = $"{Reference},  {Reference.SubReferences.Count} references:";
				foreach (Reference reference in Reference.SubReferences)
				{
					tip += $"\n  {reference.SubReferences[0]}";
				}

				return tip;
			}
		}


		public Brush LinkBrush { get; private set; }

		public double X1 => sourcePoint.X;
		public double Y1 => sourcePoint.Y;
		public double X2 => targetPoint.X;
		public double Y2 => targetPoint.Y;

		public double SubLinkCount => Reference.SubReferences.Count; 

		public override bool CanBeShown()
		{
			return 
				SourceNode.CanBeShown() && TargetNode.CanBeShown()
				&& ParentNode.ViewScale > 2 
				&& linkViewModel.StrokeThickness > 0.5;
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

			if (SourceNode == TargetNode.ParentNode)
			{
				Rect targetRect = TargetNode.RelativeNodeBounds;

				x1 = SourceNode.ActualNodeBounds.Width / 2;
				y1 = 0;
				x2 = targetRect.X + targetRect.Width / 2;
				y2 = targetRect.Y;
				LinkBrush = TargetNode.RectangleBrush;
			}
			else if (SourceNode.ParentNode == TargetNode.ParentNode)
			{
				Rect sourceRect = SourceNode.RelativeNodeBounds;
				Rect targetRect = TargetNode.RelativeNodeBounds;

				x1 = sourceRect.X + sourceRect.Width / 2;
				y1 = sourceRect.Y + sourceRect.Height;
				x2 = targetRect.X + targetRect.Width / 2;
				y2 = targetRect.Y;
				LinkBrush = SourceNode.RectangleBrush;
			}
			else if (SourceNode.ParentNode == TargetNode)
			{
				Rect sourceRect = SourceNode.RelativeNodeBounds;

				x1 = sourceRect.X + sourceRect.Width / 2;
				y1 = sourceRect.Y + sourceRect.Height;
				x2 = TargetNode.ActualNodeBounds.Width / 2;
				y2 = TargetNode.ActualNodeBounds.Height;
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

			bounds.Scale(NodeScaleFactor, NodeScaleFactor);
			ActualNodeBounds = bounds;
		}


		public override string ToString() => Reference.ToString();
	}
}