using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews.Private;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class Node : IItem
	{
		private readonly INodeItemService nodeItemService;
		//private readonly IItemService itemService;

		private static readonly Size DefaultSize = new Size(200, 100);

		public Rect ItemCanvasBounds { get; private set; }
		public double ZIndex { get; private set; }
		public double Priority { get; private set; }
		public ViewModel ViewModel { get; private set; }
		public object ItemState { get; set; }

		//private NodeItemsSource nodeItemsSource;
		private double thisNodeScaleFactor = 7;
		private double canvasScale = 1;


		//private bool isAdded = false;


		public Node(
			INodeItemService nodeItemService,
			Node parent,
			NodeName name, 
			NodeType type)
		{
			this.nodeItemService = nodeItemService;
			//this.itemService = itemService;
			ParentNode = parent;
			NodeName = name;
			NodeType = type;
			//Links = new NodeLinks(this);
		}


		public double NodeScale
		{
			get
			{
				if (ParentNode == null)
				{
					return canvasScale / thisNodeScaleFactor;
				}

				return ParentNode.NodeScale / thisNodeScaleFactor;
			}
		}


		//public override ViewModel ViewModel => viewModel;

		public Node ParentNode { get; }

		public NodeName NodeName { get; }

		public NodeType NodeType { get; private set; }

	//	public NodeLinks Links { get; }

		//public VirtualItemsSource VirtualItemsSource => nodeItemsSource;

		public NodeViewModel ModuleViewModel => ViewModel as NodeViewModel;
		public Brush RectangleBrush { get; private set; }
		public Brush BackgroundBrush { get; private set; }

		public List<Node> ChildNodes { get; } = new List<Node>();

		//public List<Link> LinkItems => ChildItems.OfType<Link>();

		public Rect? ElementBounds { get; set; }


		public void Zoom(Double scale)
		{
			canvasScale = scale;
			UpdateScale();		
		}


		private void UpdateScale()
		{
			if (ViewModel is NodeWithChildrenViewModel vm)
			{
				vm.UpdateZoomScale();
			}

			foreach (Node childNode in ChildNodes)
			{
				childNode.UpdateScale();
			}		
		}


		public void SetBounds(Rect bounds)
		{
			ItemCanvasBounds = bounds;		

			RectangleBrush = nodeItemService.GetRectangleBrush();
			BackgroundBrush = nodeItemService.GetRectangleBackgroundBrush(RectangleBrush);

			if (ChildNodes.Any())
			{
				//nodeItemsSource = new NodeItemsSource();
				ViewModel = new NodeWithChildrenViewModel(this);
				AddModuleChildren();
			}
			else
			{
				ViewModel = new NodeLeafViewModel(this);
			}
		}


		public bool CanBeShown()
		{
			return true;
				//ItemViewSize.Width > 10
				//&& (ParentItem?.ItemCanvasBounds.Contains(ItemCanvasBounds) ?? true);
		}

		public void SetType(NodeType nodeType)
		{
			NodeType = nodeType;
		}


		public void AddChild(Node child)
		{
			ChildNodes.Add(child);
		}


		public void ItemRealized()
		{
			//if (!IsRealized)
			//{
			//	base.ItemRealized();

			//	if (!isAdded)
			//	{
			//		isAdded = true;
			//		AddModuleChildren();
			//		AddLinks();
			//	}

			//	ShowChildren();
			//}
		}



		public void ChangedScale()
		{
			//base.ChangedScale();
		}


		public void ItemVirtualized()
		{
			//if (IsRealized)
			//{
			//	HideChildren();
			//	base.ItemVirtualized();
			//	//ParentNode?.RemoveChildNode(this);
			//}
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

			double xMargin = 10;
			double yMargin = 50;


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

				if (ViewModel is NodeWithChildrenViewModel vm)
				{
					vm.Add(childNode);
				}

				count++;
			}
		}

	
		//private void AddLinks()
		//{
		//	foreach (Link link in Links)
		//	{
		//		AddLink(link);
		//	}
		//}


		//private void AddLink(Link link)
		//{
		//	if (link.Source == this)
		//	{	
		//		AddChildItem(link);
		//		link.UpdateLinkLine();
		//	}
		//}


		//public void UpdateLinksFor(Item item)
		//{
		//	IEnumerable<Link> links = Links
		//		.Where(link => link.Source == item || link.Target == item)
		//		.ToList();

		//	foreach (Link link in links)
		//	{
		//		link.UpdateLinkLine();
		//		link.NotifyAll();
		//	}
		//}


		//public void UpdateLinksFor()
		//{
		//	IEnumerable<Link> links = Links	
		//		.ToList();

		//	foreach (Link link in links)
		//	{
		//		link.UpdateLinkLine();
		//		link.NotifyAll();
		//	}
		//}

	}
}