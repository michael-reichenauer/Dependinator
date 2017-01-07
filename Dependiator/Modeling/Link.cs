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


		public Link(
			INodeService nodeService,
			Reference reference,
			Rect bounds,
			Module owner)
			: base(nodeService, owner)
		{
			Reference = reference;

			this.nodeService = nodeService;

			ActualNodeBounds = bounds;

			LinkBrush = nodeService.GetNextBrush();
			ViewModel = new LinkViewModel(this);
		}


		public Reference Reference { get; }


		public override ViewModel ViewModel { get; }

		public Brush LinkBrush { get; }


		public override bool CanBeShown()
		{
			return true;
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