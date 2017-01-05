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
			Rect bounds,
			Module parent)		
			: base(nodeService, parent)
		{
			this.nodeService = nodeService;
			this.element = element;

			ActualNodeBounds = bounds;

			//Name = new ModuleName(nodeService, element.Name, this);
			//AddChildNode(Name);
	
			//AddModuleChildren();			

			RectangleBrush = nodeService.GetNextBrush();
		}

		public override ItemViewModel ViewModelFactory() => new ModuleViewModel(this);

		public string Name => element.Name;

		public string FullName => element.FullName + $" ({Children.Count()}) Level; {NodeLevel} Scale: {NodeScale} ";



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
			// Original size new Size(200, 120)
			
			int rowLength = 6;
			//Size size = new Size(1100, 1100 * 0.50);
			Size parentSize = ParentNode?.ActualNodeBounds.Size ?? new Size(200, 100);
			Size size = parentSize;

			//size = new Size((parentSize.Width / NodeScale) * 0.8, (parentSize.Height / NodeScale) * 0.8);

			int childCount = element.ChildElements.Count();

			//int columnLength = 1;


			////if (NodeLevel == 0)
			//{
			//	if (childCount > 20)
			//	{
			//		rowLength = 6;
			//		/*columnLength = 5*/;
			//		size = new Size(200, 120);
			//	}
			//	else if (childCount > 12)
			//	{
			//		rowLength = 5;
			//		//columnLength = 4;
			//		size = new Size(250, 120);
			//	}
			//	else if (childCount > 6)
			//	{
			//		rowLength = 4;
			//		//columnLength = 3;
			//		size = new Size(310, 160);
			//	}
			//	else if (childCount > 2)
			//	{
			//		rowLength = 3;
			//		//columnLength = 2;
			//		size = new Size(430, 280);
			//	}
			//	else if (childCount > 1)
			//	{
			//		rowLength = 2;
			//		//columnLength = 1;
			//		size = new Size(600, 400);
			//	}
			//}

			//double subWidth = ((((parentSize.Width) * NodeScaleFactor) / rowLength) * 0.75) / NodeScale;
			//double subHeight = subWidth / 2;

			//try
			//{
			//	size = new Size(subWidth, subHeight);
			//}
			//catch (Exception e)
			//{
			//	Console.WriteLine(e);
			//	throw;
			//}
			

			//}
			//if (NodeLevel == 1)
			//{
			//	if (childCount > 12)
			//	{
			//		rowLength = 5;
			//		size = new Size(100, 70);
			//	}
			//	else if (childCount > 6)
			//	{
			//		rowLength = 4;
			//		size = new Size(310, 160);
			//	}
			//	else if (childCount > 2)
			//	{
			//		rowLength = 3;
			//		size = new Size(2450, 1500);
			//	}
			//	else if (childCount > 1)
			//	{
			//		rowLength = 2;
			//		size = new Size(600, 400);
			//	}
			//}

			//double nscale = ParentNode?.NodeScale ?? 1;

			//if (NodeLevel == 1)
			//{
			//	size = new Size(size.Width * ((NodeLevel) * 2), size.Height * ((NodeLevel) * 2));
			//}

			//if (NodeLevel >=  2)
			//{
			//	return;
			//}

			//if (NodeLevel == 2)
			//{
			//	size = new Size(size.Width * ((NodeLevel) * 60), size.Height * ((NodeLevel) * 60));
			//}

			int count = 0;
			foreach (Element childElement in element.ChildElements)
			{
				int x = count % rowLength;
				int y = count / rowLength;

				Point position = new Point(x * (size.Width + 20) + 30, y * (size.Height + 20) + 160);
				
				Rect bounds = new Rect(position, size);

				Module module = new Module(nodeService, childElement, bounds, this);
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