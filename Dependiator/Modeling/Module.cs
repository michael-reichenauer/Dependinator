using System;
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
		static Random random = new Random(1234);

		private readonly INodeService nodeService;
		private readonly int nbrOfChildModules;

		public Module(
			INodeService nodeService,
			string name,
			Point position,
			Module parent)		
			: base(nodeService, parent)
		{
			this.nodeService = nodeService;
			nbrOfChildModules = random.Next(0, 20);

			ActualNodeBounds = new Rect(position, new Size(100, 60));

			NodeScaleFactor = 7;

			Name = new ModuleName(nodeService, name, this);
			AddChildNode(Name);
	
			//AddModuleChildren();			

			RectangleBrush = nodeService.GetNextBrush();
		}

		public override ItemViewModel ViewModelFactory() => new ModuleViewModel(this);
		
		public ModuleName Name { get; }



		public ModuleViewModel ModuleViewModel => ViewModel as ModuleViewModel;
		public Brush RectangleBrush { get; }

		public IEnumerable<Module> Children => ChildNodes.OfType<Module>();

		public override bool CanBeShown()
		{
			return ViewNodeSize.Width > 20 && (ParentNode?.ItemBounds.Contains(ItemBounds) ?? true);
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


		public override void ChangedScale()
		{
			
			base.ChangedScale();
		}


		public override void ItemVirtualized()
		{
			if (IsRealized)
			{
				HideChildren();
				base.ItemVirtualized();
				//ParentNode?.RemoveChildNode(this);
			}
		}



		private void AddModuleChildren()
		{		
			int total = 5;
			int count = 0;
			for (int y = 0; y < total - 1; y++)
			{
				for (int x = 0; x < total; x++)
				{
					AddChildNode(new Module(
						nodeService, 
						$"Name {x},{y}",
						new Point(x * 130 + 30, y * 70 + 100), this));

					if (count++ > nbrOfChildModules)
					{
						return;
					}
				}
			}
		}


		private void RemoveModuleChildren()
		{
			foreach (var child in Children.ToList())
			{
				RemoveChildNode(child);
			}			
		}
	}
}