using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Analyzing;
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

			//Name = new ModuleName(nodeService, element.Name, this);
			//AddChildNode(Name);

			//AddModuleChildren();			

			RectangleBrush = nodeService.GetRectangleBrush();
			BackgroundBrush = nodeService.GetRectangleBackgroundBrush(RectangleBrush);
			ViewModel = new ModuleViewModel(this);
		}


		public Element Element { get; }

		public override ViewModel ViewModel { get; }


		public string Name => Element.Name.Name;

		public string FullName =>
			Element.Name.FullName +
			$"\nchildren: {ChildModules.Count()}, decedents: {Element.Children.Descendents().Count()}\n" +
			$"SourceRefs {Element.References.DescendentAndSelfSourceReferences().Count()} " +
			$"TargetRefs {Element.References.DescendentAndSelfTargetReferences().Count()}";


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
					AddReferences();
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

			int count = 0;
			foreach (Element childElement in Element.Children)
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


		private void AddReferences()
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
	}
}