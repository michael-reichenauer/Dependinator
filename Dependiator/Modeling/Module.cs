using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class Module : Node
	{
		public Module(
			INodeService nodeService,
			string name,
			Point position)		
			: base(nodeService, null)
		{
			RelativeBounds = new Rect(position, new Size(100, 50));

			Name = new ModuleName(nodeService, name, new Point(5, 5));
			AddChild(Name);
			nodeService.AddRootNode(this);

			RectangleBrush = nodeService.GetNextBrush();
		}

		public override ItemViewModel ViewModelFactory() => new ModuleViewModel(this);
		
		public ModuleName Name { get; }

		public Module Parent { get; }

		public IReadOnlyList<Module> Children { get; } = new List<Module>();

		public ModuleViewModel ModuleViewModel => ViewModel as ModuleViewModel;
		public Brush RectangleBrush { get; }


		internal IEnumerable<Node> GetShowableNodes()
		{
			yield return this;
			yield return Name;
		}


		public override void TryAddNode()
		{
			if (Scale < 0.4)
			{
				return;
			}

			ShowNode();				
		}

		public override void ChangedScale()
		{
			if (Scale < 0.4)
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