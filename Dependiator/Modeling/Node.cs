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
		public static string NameSpaceType = "NameSpace";
		public static readonly string TypeType = "Type";
		public static readonly string MemberType = "Member";
		private static readonly Size DefaultSize = new Size(200, 100);

		private readonly IItemService itemService;

		private ViewModel viewModel;


		public Node(
			IItemService itemService,
			Node parent,
			NodeName name, 
			string type)
			: base(itemService, parent)
		{
			NodeName = name;
			NodeType = type;
			NodeLinks = new NodeLinks(itemService, this);
			this.itemService = itemService;	
		}


		public void SetBounds(Rect bounds)
		{
			ItemBounds = bounds;

			//RectangleBrush = Element.ElementBrush ?? itemService.GetRectangleBrush();
			RectangleBrush = itemService.GetRectangleBrush();
			BackgroundBrush = itemService.GetRectangleBackgroundBrush(RectangleBrush);
			viewModel = new NodeViewModel(this);
		}



		public override ViewModel ViewModel => viewModel;

		public Node ParentNode => (Node)ParentItem;
		public string Name => NodeName.Name;

		public NodeName NodeName { get; }

		public string NodeType { get; private set; }

		public NodeLinks NodeLinks { get; }


		public string FullName =>
			$"{NodeName.FullName}\n" +
			$"Scale: {CanvasScale:#.##}, Level: {ItemLevel}, NodeScale: {ItemScale:#.##}, NSF: {ThisItemScaleFactor}";


		public NodeViewModel ModuleViewModel => ViewModel as NodeViewModel;
		public Brush RectangleBrush { get; private set; }
		public Brush BackgroundBrush { get; private set; }

		public IEnumerable<Node> ChildNodes => ChildItems.OfType<Node>();

		public IEnumerable<Link> LinkItems => ChildItems.OfType<Link>();

		public Rect? ElementBounds { get; set; }


		public override bool CanBeShown()
		{
			return ItemViewSize.Width > 10 && (ParentItem?.ItemCanvasBounds.Contains(ItemCanvasBounds) ?? true);
		}

		public void SetType(string nodeType)
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

				//if (!ChildNodeItems.Any())
				{
					AddModuleChildren();
				}

				//if (!LinkItemss.Any())
				{
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


		public override string ToString() => NodeName.FullName;

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
			var children = ChildNodes.OrderBy(e => e, Compare.With<Node>(CompareElements));

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

				//Node node = new Node(itemService, childElement, this);
				childNode.SetBounds(bounds);
				count++;
			}
		}


		private int CompareElements(Node e1, Node e2)
		{
			Link e1ToE2 = NodeLinks
				.FirstOrDefault(r => r.Source == e1 && r.Target == e2);
			Link e2ToE1 = NodeLinks
				.FirstOrDefault(r => r.Source == e2 && r.Target == e1);

			int e1ToE2Count = e1ToE2?.NodeLinks.Count ?? 0;
			int e2ToE1Count = e2ToE1?.NodeLinks.Count ?? 0;

			if (e1ToE2Count > e2ToE1Count)
			{
				return -1;
			}
			else if (e1ToE2Count < e2ToE1Count)
			{
				return 1;
			}

			Link parentToE1 = NodeLinks
				.FirstOrDefault(r => r.Source == this && r.Target == e1);
			Link parentToE2 = NodeLinks
				.FirstOrDefault(r => r.Source == this && r.Target == e2);

			int parentToE1Count = parentToE1?.NodeLinks.Count ?? 0;
			int parentToE2Count = parentToE2?.NodeLinks.Count ?? 0;

			if (parentToE1Count > parentToE2Count)
			{
				return -1;
			}
			else if (parentToE1Count < parentToE2Count)
			{
				return 1;
			}

			Link e1ToParent = NodeLinks
				.FirstOrDefault(r => r.Source == e1 && r.Target == this);
			Link e2ToParent = NodeLinks
				.FirstOrDefault(r => r.Source == e2 && r.Target == this);

			int e1ToParentCount = e1ToParent?.NodeLinks.Count ?? 0;
			int e2ToParentCount = e2ToParent?.NodeLinks.Count ?? 0;

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
			foreach (Link link in NodeLinks)
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
			IEnumerable<Link> links = NodeLinks
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
			IEnumerable<Link> links = NodeLinks	
				.ToList();

			foreach (Link link in links)
			{
				link.UpdateLinkLine();
				link.NotifyAll();
			}
		}
	}
}