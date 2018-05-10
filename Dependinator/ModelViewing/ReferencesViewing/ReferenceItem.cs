using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItem
	{
		public ReferenceItem(
			IReferenceItemService referenceItemService,
			Node node,
			bool isIncoming,
			Node baseNode)
		{
			this.ItemService = referenceItemService;
			Node = node;
			IsIncoming = isIncoming;
			BaseNode = baseNode;
		}


		public Node Node { get; }
		public bool IsIncoming { get; }
		public Node BaseNode { get; }
		public Link Link { get; set; }
		public List<ReferenceItem> SubItems { get; } = new List<ReferenceItem>();
		public ReferenceItem Parent { get; private set; }

		public Brush ItemTextBrush() => ItemService.ItemTextBrush();
		public Brush ItemTextHiddenBrush() => ItemService.ItemTextHiddenBrush();


		public IReferenceItemService ItemService { get; }


		public void AddChild(ReferenceItem child)
		{
			child.Parent = this;
			SubItems.Add(child);
		}


		public void AddChildren(IEnumerable<ReferenceItem> child) => child.ForEach(AddChild);


		public override string ToString() => $"{Node}";


		public IEnumerable<ReferenceItem> GetReferences(bool isIncoming)
		{
			return ItemService.GetReferences(Node, new ReferenceOptions(isIncoming, BaseNode));
		}
	}
}