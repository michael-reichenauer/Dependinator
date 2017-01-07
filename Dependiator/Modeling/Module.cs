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
			foreach (Reference reference in Element.References)
			{
				Node sourceNode;
				Node targetNode;
				double x1;
				double y1;
				double x2;
				double y2;

				if (reference.SubReferences.Any(r => r.Kind == ReferenceKind.Child))
				{
					sourceNode = this;
					targetNode = ChildModules.First(m => m.Element == reference.Target);
					Rect targetRect = targetNode.RelativeNodeBounds;

					x1 = ActualNodeBounds.Width / 2;
					y1 = 0;
					x2 = targetRect.X + targetRect.Width / 2;
					y2 = targetRect.Y;
				}
				else if (reference.Source != Element
				         && reference.Target != Element
				         && reference.SubReferences.Any(r => r.Kind == ReferenceKind.Sibling))
				{
					sourceNode = ChildModules.First(m => m.Element == reference.Source);
					targetNode = ChildModules.First(m => m.Element == reference.Target);
					Rect sourceRect = sourceNode.RelativeNodeBounds;
					Rect targetRect = targetNode.RelativeNodeBounds;

					x1 = sourceRect.X + sourceRect.Width / 2;
					y1 = sourceRect.Y + sourceRect.Height;
					x2 = targetRect.X + targetRect.Width / 2;
					y2 = targetRect.Y;
				}
				else if (reference.SubReferences.Any(r => r.Kind == ReferenceKind.Parent))
				{
					sourceNode = ChildModules.First(m => m.Element == reference.Source);
					targetNode = this;
					Rect sourceRect = sourceNode.RelativeNodeBounds;
					
					x1 = sourceRect.X + sourceRect.Width / 2;
					y1 = sourceRect.Y + sourceRect.Height;
					x2 = ActualNodeBounds.Width / 2;
					y2 = ActualNodeBounds.Height;
				}
				else
				{
					continue;
				}

				double x = Math.Min(x1, x2);
				double y = Math.Min(y1, y2);
				double width = Math.Abs(x2 - x1);
				double height = Math.Abs(y2 - y1);

				Point source;
				Point target;

				if (x1 <= x2 && y1 <= y2)
				{
					source = new Point(0, 0);
					target = new Point(width, height);
				}
				else if (x1 <= x2 && y1 > y2)
				{
					source = new Point(0, height);
					target = new Point(width, 0);
				}
				else if (x1 > x2 && y1 <= y2)
				{
					source = new Point(width, 0);
					target = new Point(0, height);
				}
				else
				{
					source = new Point(width, height);
					target = new Point(0, 0);
				}

				Rect bounds = new Rect(new Point(x, y), new Size(width + 1, height + 1));

				bounds.Scale(NodeScaleFactor, NodeScaleFactor);

				Link link = new Link(nodeService, reference, bounds, source, target, this, sourceNode, targetNode);
				AddChildNode(link);
			}
		}
	}
}