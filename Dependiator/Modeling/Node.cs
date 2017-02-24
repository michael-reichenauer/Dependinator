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
	internal class Node : Item
	{		
		private static readonly Size DefaultSize = new Size(200, 100);

		private readonly IItemService itemService;

		private ViewModel viewModel;
		private bool isAdded = false;

		public Node(
			IItemService itemService,
			Node parent,
			NodeName name, 
			NodeType type)
			: base(itemService, parent)
		{
			NodeName = name;
			NodeType = type;
			Links = new NodeLinks(itemService, this);
			this.itemService = itemService;	
		}


		public override ViewModel ViewModel => viewModel;

		public Node ParentNode => (Node)ParentItem;

		public NodeName NodeName { get; }

		public NodeType NodeType { get; private set; }

		public NodeLinks Links { get; }
	

		public NodeViewModel ModuleViewModel => ViewModel as NodeViewModel;
		public Brush RectangleBrush { get; private set; }
		public Brush BackgroundBrush { get; private set; }

		public IEnumerable<Node> ChildNodes => ChildItems.OfType<Node>();

		public IEnumerable<Link> LinkItems => ChildItems.OfType<Link>();

		public Rect? ElementBounds { get; set; }


		public void SetBounds(Rect bounds)
		{
			ItemBounds = bounds;

			//RectangleBrush = Element.ElementBrush ?? itemService.GetRectangleBrush();
			RectangleBrush = itemService.GetRectangleBrush();
			BackgroundBrush = itemService.GetRectangleBackgroundBrush(RectangleBrush);
			viewModel = new NodeViewModel(this);
		}


		public override bool CanBeShown()
		{
			return 
				ItemViewSize.Width > 10
				&& (ParentItem?.ItemCanvasBounds.Contains(ItemCanvasBounds) ?? true);
		}

		public void SetType(NodeType nodeType)
		{
			NodeType = nodeType;
		}


		public void AddChild(Node child)
		{
			AddChildItem(child);
		}


		public override void ItemRealized()
		{
			if (!IsRealized)
			{
				base.ItemRealized();

				if (!isAdded)
				{
					isAdded = true;
					AddModuleChildren();
					AddLinks();
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


		public override string ToString() => NodeName;

		public IEnumerable<Node> Ancestors()
		{
			Node current = ParentNode;

			while (current != null)
			{
				yield return current;
				current = current.ParentNode;
			}
		}

		public IEnumerable<Node> AncestorsAndSelf()
		{
			yield return this;

			foreach (Node ancestor in Ancestors())
			{
				yield return ancestor;
			}
		}

		public IEnumerable<Node> Descendents()
		{
			foreach (Node child in ChildNodes)
			{
				yield return child;

				foreach (Node descendent in child.Descendents())
				{
					yield return descendent;
				}
			}
		}

		public IEnumerable<Node> DescendentsAndSelf()
		{
			yield return this;

			foreach (Node descendent in Descendents())
			{
				yield return descendent;
			}
		}

		private void AddModuleChildren()
		{	
			int rowLength = 6;

			int padding = 20;

			double xMargin = ((DefaultSize.Width * ThisItemScaleFactor) - ((DefaultSize.Width + padding) * rowLength)) / 2;
			double yMargin = 25 * ThisItemScaleFactor;

			if (ParentItem == null)
			{
				xMargin += ItemBounds.Width / 2;
				yMargin += ItemBounds.Height / 2;
			}

			int count = 0;
			var children = ChildNodes.OrderBy(child => child, NodeComparer.Comparer(this));

			foreach (Node childNode in children)
			{
				//Size size = childElement.ElementBounds?.Size ?? DefaultSize;
				Size size =  DefaultSize;

				Point location;
				//if (childElement.ElementBounds.HasValue)
				//{
				//	location = childElement.ElementBounds.Value.Location;
				//}
				//else
				{
					int x = count % rowLength;
					int y = count / rowLength;
					location = new Point(x * (DefaultSize.Width + padding) + xMargin, y * (DefaultSize.Height + padding) + yMargin);
				}

				Rect bounds = new Rect(location, size);
				childNode.SetBounds(bounds);

				count++;
			}
		}

	
		private void AddLinks()
		{
			foreach (Link link in Links)
			{
				AddLink(link);
			}
		}


		private void AddLink(Link link)
		{
			if (link.Source == this)
			{	
				AddChildItem(link);
				link.UpdateLinkLine();
			}
		}


		public void UpdateLinksFor(Item item)
		{
			IEnumerable<Link> links = Links
				.Where(link => link.Source == item || link.Target == item)
				.ToList();

			foreach (Link link in links)
			{
				link.UpdateLinkLine();
				link.NotifyAll();
			}
		}


		public void UpdateLinksFor()
		{
			IEnumerable<Link> links = Links	
				.ToList();

			foreach (Link link in links)
			{
				link.UpdateLinkLine();
				link.NotifyAll();
			}
		}
	}
}