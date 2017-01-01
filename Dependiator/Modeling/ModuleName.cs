using System;
using System.Windows;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class ModuleName : Node
	{
		public ModuleName(INodeService nodeService, string name, Point position) 
			: base(nodeService, null)
		{
			RelativeBounds = new Rect(position, new Size(70, 20));
			Name = name;
		}

		public override ItemViewModel ViewModelFactory() => new ModuleNameViewModel(this);

		public string Name { get; }


		public override void TryAddNode()
		{
			if (Scale < 0.6)
			{
				return;
			}

			ShowNode();
		}


		public override void ChangedScale()
		{
			if (Scale < 0.5)
			{
				HideNode();
			}
			else
			{
				base.ChangedScale();
			}
		}
	}
}