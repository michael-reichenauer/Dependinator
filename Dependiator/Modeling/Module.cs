using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Module : Node
	{
		private readonly INodeService nodeService;


		public Module(
			INodeService nodeService,
			Element element,
			Rect bounds,
			Module parent)		
			: base(nodeService, parent)
		{
			Element = element;
			this.nodeService = nodeService;

			ActualNodeBounds = bounds;

			//Name = new ModuleName(nodeService, element.Name, this);
			//AddChildNode(Name);
	
			//AddModuleChildren();			

			RectangleBrush = nodeService.GetNextBrush();
			ViewModel = new ModuleViewModel(this);
		}


		public Element Element { get; }

		public override ViewModel ViewModel { get; }


		public string Name => Element.Name.Name;

		public string FullName => Element.Name.FullName +
			$"\nchildren: {ChildModules.Count()}, decedents: {Element.Children.Descendents().Count()}\n" +
			$"SourceRefs {Element.References.DescendentAndSelfSourceReferences().Count()} " +
			$"TargetRefs {Element.References.DescendentAndSelfTargetReferences().Count()}";



		public ModuleViewModel ModuleViewModel => ViewModel as ModuleViewModel;
		public Brush RectangleBrush { get; }

		public IEnumerable<Module> ChildModules => ChildNodes.OfType<Module>();

		public IEnumerable<Link> Links => ChildNodes.OfType<Link>();

		public override bool CanBeShown()
		{
			return ViewNodeSize.Width > 20 && (ParentNode?.ItemBounds.Contains(ItemBounds) ?? true);
		}

		public override void ItemRealized()
		{
			if (!IsRealized)
			{
				base.ItemRealized();
				if (!ChildModules.Any())
				{
					AddModuleChildren();
					
				}
				if (!Links.Any())
				{
					AddReferences();
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


		public override string ToString() => Element.Name.FullName;
		


		private void AddModuleChildren()
		{
			// Original size new Size(200, 120)		
			int rowLength = 6;

			Size size = ActualNodeBounds.Size;
			int padding = 20;

			double xMargin = ((size.Width * NodeScaleFactor) - ((size.Width + padding) * rowLength)) / 2;
			double yMargin = 25 * NodeScaleFactor;

			int count = 0;
			foreach (Element childElement in Element.Children)
			{
				int x = count % rowLength;
				int y = count / rowLength;

				Point position = new Point(x * (size.Width + padding) + xMargin, y * (size.Height + padding) + yMargin);
				
				Rect bounds = new Rect(position, size);

				Module module = new Module(nodeService, childElement, bounds, this);
				AddChildNode(module);
				count++;
			}
		}


		private void AddReferences()
		{
			int count = Element.References.Count();

			foreach (Reference reference in Element.References)
			{
				Reference childReference = reference.SubReferences
					.FirstOrDefault(r => r.Kind == ReferenceKind.Child);

				if (childReference != null)
				{
					Node sourceNode = this;
					Node targetNode = ChildModules.First(m => m.Element == reference.Target);

					Rect targetRect = targetNode.RelativeNodeBounds;
					double x1 = ActualNodeBounds.Width / 2;
					double y1 = 0;

					double x2 = targetRect.X + targetRect.Width / 2;
					double y2 = targetRect.Y;

					Rect bounds;
					Point source;
					Point target;
					if (x1 < x2)
					{
						source = new Point(0, 0);
						target = new Point(x2 - x1, y2 - y1);
						bounds = new Rect(new Point(x1, y1), new Size(target.X + 1, target.Y + 1));
					}
					else
					{
						source = new Point(x1 - x2, y1);
						target = new Point(0, y2 - y1);
						bounds = new Rect(new Point(x2, y1), new Size(x1 - x2 + 1, y2 - y1 + 1));
					}

					bounds.Scale(NodeScaleFactor, NodeScaleFactor);

					Link link = new Link(nodeService, reference, bounds, source, target, this, sourceNode, targetNode);
					AddChildNode(link);
				}

				//if (reference.SubReferences.Any(r => r.Kind != ReferenceKind.Direkt))
				//{
				//	Rect rec = new Rect(
				//		new Point(50, 10),
				//		new Size(50, 50));
				//	rec.Scale(NodeScaleFactor, NodeScaleFactor);

				//	//Size size = new Size((ActualNodeBounds.Width - 20) * NodeScaleFactor, (ActualNodeBounds.Height - 20) * NodeScaleFactor);
				//	//Point location = new Point(10 * NodeScaleFactor, 10 * NodeScaleFactor);
				//	//Rect bounds = new Rect(location, size);

				//	Link link = new Link(nodeService, reference, rec, new Point(0, 0), new Point(48, 48), this);
				//	AddChildNode(link);
				//}			
			}
		}
	}
}