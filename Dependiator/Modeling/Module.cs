using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	internal class Module : Node
	{
		private readonly INodeService nodeService;
		private readonly Element element;


		public Module(
			INodeService nodeService,
			Element element,
			Point position,
			Module parent)		
			: base(nodeService, parent)
		{
			this.nodeService = nodeService;
			this.element = element;

			ActualNodeBounds = new Rect(position, new Size(100, 60));

			NodeScaleFactor = 7;

			Name = new ModuleName(nodeService, element.Name, this);
			AddChildNode(Name);
	
			//AddModuleChildren();			

			RectangleBrush = nodeService.GetNextBrush();
		}

		public override ItemViewModel ViewModelFactory() => new ModuleViewModel(this);
		
		public ModuleName Name { get; }

		public string FullName => element.FullName;



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
			int count = 0;
			foreach (Element childElement in element.ChildElements)
			{
				int x = count % 5;
				int y = count / 5;

				Module module = new Module(nodeService, childElement, new Point(x * 130 + 30, y * 70 + 100), this);
				AddChildNode(module);
				count++;
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