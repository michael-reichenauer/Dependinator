using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItem
	{
		private readonly string text;


		public ReferenceItem(
			IReferenceItemService referenceItemService,
			Node node,
			bool isIncoming,
			Node baseNode,
			bool isSubReference,
			string text = null)
		{
			this.text = text;
			this.ItemService = referenceItemService;
			Node = node;
			IsIncoming = isIncoming;
			BaseNode = baseNode;
			IsSubReference = isSubReference;
		}


		public string Text => text ?? GetText();
		public bool IsTitle => text != null;
		public Node Node { get; }
		public bool IsIncoming { get; }
		public Node BaseNode { get; }
		public bool IsSubReference { get; }
		public Link Link { get; set; }
		public List<ReferenceItem> SubItems { get; } = new List<ReferenceItem>();
		public ReferenceItem Parent { get; set; }

		public Brush ItemTextBrush() => IsSubReference ? 
			ItemService.ItemTextLowBrush() :  ItemService.ItemTextBrush();
		public Brush ItemTextHiddenBrush() => ItemService.ItemTextHiddenBrush();


		public IReferenceItemService ItemService { get; }
		public string ToolTip => text ?? Node.Name.DisplayFullNameWithType;


		public void AddChild(ReferenceItem child)
		{
			child.Parent = this;
			SubItems.Add(child);
		}


		public void AddChildren(IEnumerable<ReferenceItem> child) => child.ForEach(AddChild);


		public override string ToString() => $"{Text}";


		public IEnumerable<ReferenceItem> GetSubReferences(bool isIncoming)
		{
			return ItemService.GetReferences(Node, new ReferenceOptions(isIncoming, BaseNode, true));
		}

		private string GetText()
		{
			if (Parent != null && Parent.Node == Node.Parent)
			{
				return Node.Name.DisplayName;
			}

			return Node.Name.DisplayFullNoParametersName;
		}
	}
}