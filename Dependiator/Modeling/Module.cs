using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	internal class Module : Node
	{
		private readonly INodeService nodeService;


		public Module(
			INodeService nodeService,
			string name,
			Point position,
			Module parent)		
			: base(nodeService, parent)
		{
			this.nodeService = nodeService;

			ActualNodeBounds = new Rect(position, new Size(100, 50));

			//Name = new ModuleName(nodeService, name, this);
	
			//AddModuleChildren();			

			RectangleBrush = parent?.RectangleBrush ?? nodeService.GetNextBrush();
		}

		public override ItemViewModel ViewModelFactory() => new ModuleViewModel(this);
		
		public ModuleName Name { get; }



		public ModuleViewModel ModuleViewModel => ViewModel as ModuleViewModel;
		public Brush RectangleBrush { get; }

		public IEnumerable<Module> Children => ChildNodes.OfType<Module>();

		public override bool CanBeShown()
		{
			return ViewNodeSize.Width > 40;
		}

		public override void ItemRealized()
		{
			if (!IsRealized)
			{
				base.ItemRealized();
				if (!Children.Any())
				{
					AddModuleChildren();			
				}

				ShowChildren();
			}
		}

		public override void ItemVirtualized()
		{
			if (IsRealized)
			{
				HideChildren();
				base.ItemVirtualized();
				//ParentNode?.RemoveChild(this);
			}
		}



		private void AddModuleChildren()
		{
			int total = 2;

			for (int y = 0; y < total; y++)
			{
				for (int x = 0; x < total; x++)
				{
					AddChild(new Module(nodeService, $"Name {x},{y}", new Point(x * 200 + 10, y * 100 + 10), this));
				}
			}
		}


		private void RemoveModuleChildren()
		{
			foreach (var child in Children.ToList())
			{
				RemoveChild(child);
			}			
		}
	}
}