using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Utils;


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
			ActualNodeBounds = new Rect(position, new Size(100, 50));

			Name = new ModuleName(nodeService, name);
			Name.ActualNodeBounds = new Rect(0, 0, ActualNodeBounds.Width, 20);

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



		public override bool CanBeShown()
		{
			return ViewNodeBounds.Width > 40;
		}
	}
}