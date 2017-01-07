using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Link : Node
	{
		private readonly INodeService nodeService;
		private readonly Point source;
		private readonly Point target;
		private readonly Node sourceNode;
		private readonly Node targetNode;


		public Link(
			INodeService nodeService,
			Reference reference,
			Rect bounds,
			Point source, 
			Point target,
			Module owner,
			Node sourceNode,
			Node targetNode)
			: base(nodeService, owner)
		{
			Reference = reference;

			this.nodeService = nodeService;
			this.source = source;
			this.target = target;
			this.sourceNode = sourceNode;
			this.targetNode = targetNode;

			ActualNodeBounds = bounds;

			LinkBrush = nodeService.GetNextBrush();
			ViewModel = new LinkViewModel(this);
		}


		//private double Width => ParentNode.ActualNodeBounds.Width;
		//private double Height => ParentNode.ActualNodeBounds.Height;

		public Reference Reference { get; }


		public override ViewModel ViewModel { get; }

		public Brush LinkBrush { get; }

		public double X1 => source.X;
		public double Y1 => source.Y;
		public double X2 => target.X;
		public double Y2 => target.Y;


		public override bool CanBeShown()
		{
			return sourceNode.CanBeShown() && targetNode.CanBeShown();
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
	}
}