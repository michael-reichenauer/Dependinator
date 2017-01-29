using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Module : Node
	{
		private readonly INodeService nodeService;
		private static readonly Size DefaultSize = new Size(200, 100);


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

			RectangleBrush = nodeService.GetRectangleBrush();
			BackgroundBrush = nodeService.GetRectangleBackgroundBrush(RectangleBrush);
			ViewModel = new ModuleViewModel(this);
		}


		public Element Element { get; }

		public override ViewModel ViewModel { get; }


		public string Name => Element.Name.Name;

		public string FullName =>
			$"{Element.Name.FullName}\n" +
			$"children: {ChildModules.Count()}, decedents: {Element.Children.Descendents().Count()}\n" +
			$"Scale: {Scale:#.##}, Level: {NodeLevel}, ViewScale: {ViewScale:#.##}, NSF: {NodeScaleFactor}";


		public ModuleViewModel ModuleViewModel => ViewModel as ModuleViewModel;
		public Brush RectangleBrush { get; }
		public Brush BackgroundBrush { get; }

		public IEnumerable<Module> ChildModules => ChildNodes.OfType<Module>();

		public IEnumerable<Link> Links => ChildNodes.OfType<Link>();


		public override bool CanBeShown()
		{
			return ViewNodeSize.Width > 10 && (ParentNode?.ItemBounds.Contains(ItemBounds) ?? true);
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
					AddLinks();				
				}

				ShowChildren();
			}
		}


		protected override void SetElementBounds()
		{
			Element.SetLocationAndSize(ActualNodeBounds.Location, ActualNodeBounds.Size);
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

			int padding = 20;

			double xMargin = ((DefaultSize.Width * NodeScaleFactor) - ((DefaultSize.Width + padding) * rowLength)) / 2;
			double yMargin = 25 * NodeScaleFactor;

			if (ParentNode == null)
			{
				xMargin += ActualNodeBounds.Width / 2;
				yMargin += ActualNodeBounds.Height / 2;
			}


			int count = 0;
			var children = Element.Children.OrderBy(e => e, Compare.With<Element>(CompareElements));
			//Sorter.Sort(children, Compare.With<Element>(CompareElements));

			foreach (Element childElement in children)
			{
				Size size = childElement.Size ?? DefaultSize;

				Point location;
				if (childElement.Location != null)
				{
					location = childElement.Location.Value;
				}
				else
				{
					int x = count % rowLength;
					int y = count / rowLength;
					location = new Point(x * (DefaultSize.Width + padding) + xMargin, y * (DefaultSize.Height + padding) + yMargin);
				}

				Rect bounds = new Rect(location, size);

				Module module = new Module(nodeService, childElement, bounds, this);
				AddChildNode(module);
				count++;
			}
		}


		private int CompareElements(Element e1, Element e2)
		{
			Reference e1ToE2 = Element.References
				.FirstOrDefault(r => r.Source == e1 && r.Target == e2);
			Reference e2ToE1 = Element.References
				.FirstOrDefault(r => r.Source == e2 && r.Target == e1);

			int e1ToE2Count = e1ToE2?.SubReferences.Count ?? 0;
			int e2ToE1Count = e2ToE1?.SubReferences.Count ?? 0;

			if (e1ToE2Count > e2ToE1Count)
			{
				return -1;
			}
			else if (e1ToE2Count < e2ToE1Count)
			{
				return 1;
			}

			Reference parentToE1 = Element.References
				.FirstOrDefault(r => r.Source == Element && r.Target == e1);
			Reference parentToE2 = Element.References
				.FirstOrDefault(r => r.Source == Element && r.Target == e2);

			int parentToE1Count = parentToE1?.SubReferences.Count ?? 0;
			int parentToE2Count = parentToE2?.SubReferences.Count ?? 0;

			if (parentToE1Count > parentToE2Count)
			{
				return -1;
			}
			else if (parentToE1Count < parentToE2Count)
			{
				return 1;
			}

			Reference e1ToParent = Element.References
				.FirstOrDefault(r => r.Source == e1 && r.Target == Element);
			Reference e2ToParent = Element.References
				.FirstOrDefault(r => r.Source == e2 && r.Target == Element);

			int e1ToParentCount = e1ToParent?.SubReferences.Count ?? 0;
			int e2ToParentCount = e2ToParent?.SubReferences.Count ?? 0;

			if (e1ToParentCount > e2ToParentCount)
			{
				return -1;
			}
			else if (e1ToParentCount < e2ToParentCount)
			{
				return 1;
			}

			return 0;
		}


		private void AddLinks()
		{
			foreach (Reference reference in Element.References)
			{
				AddLink(reference);
			}
		}


		private void AddLink(Reference reference)
		{
			Module sourceNode;
			Module targetNode;

			if (reference.SubReferences.Any(r => r.Kind == ReferenceKind.Child))
			{
				sourceNode = this;
				targetNode = ChildModules.First(m => m.Element == reference.Target);
			}
			else if (reference.Source != Element
			         && reference.Target != Element
			         && reference.SubReferences.Any(r => r.Kind == ReferenceKind.Sibling))
			{
				sourceNode = ChildModules.First(m => m.Element == reference.Source);
				targetNode = ChildModules.First(m => m.Element == reference.Target);
			}
			else if (reference.SubReferences.Any(r => r.Kind == ReferenceKind.Parent))
			{
				sourceNode = ChildModules.First(m => m.Element == reference.Source);
				targetNode = this;
			}
			else
			{
				return;
			}

			Link link = new Link(nodeService, reference, this, sourceNode, targetNode);
			AddChildNode(link);
		}


		public void UpdateLinksFor(Node node)
		{
			IEnumerable<Link> links = ChildNodes
				.OfType<Link>()
				.Where(link => link.SourceNode == node || link.TargetNode == node)
				.ToList();

			foreach (Link link in links)
			{
				link.SetLinkLine();
				link.NotifyAll();
			}
		}

		public void UpdateLinksFor()
		{
			IEnumerable<Link> links = ChildNodes
				.OfType<Link>()		
				.ToList();

			foreach (Link link in links)
			{
				link.SetLinkLine();
				link.NotifyAll();
			}
		}
	}
}