using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItem
	{
		private readonly IReferenceItemService referenceItemService;


		public ReferenceItem(IReferenceItemService referenceItemService, Node node, bool isIncoming)
		{
			this.referenceItemService = referenceItemService;
			Node = node;
			IsIncoming = isIncoming;
		}


		public Node Node { get; }
		public bool IsIncoming { get; }
		public Link Link { get; set; }
		public List<ReferenceItem> Items { get; } = new List<ReferenceItem>();
		public ReferenceItem Parent { get; private set; }

		public Brush ItemTextBrush() => referenceItemService.ItemTextBrush();
		public Brush ItemTextHiddenBrush() => referenceItemService.ItemTextHiddenBrush();


		public void AddChild(ReferenceItem child)
		{
			child.Parent = this;
			Items.Add(child);
		}


		public void AddChildren(IEnumerable<ReferenceItem> child) => child.ForEach(AddChild);


		public override string ToString() => $"{Node}";
	}
}