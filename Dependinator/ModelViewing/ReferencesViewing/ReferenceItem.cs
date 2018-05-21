using System.Collections.Generic;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItem
	{
		public ReferenceItem(IReferenceItemService referenceItemService, Node node)
		{
			ItemService = referenceItemService;
			Node = node;
		}


		public string Text => GetText();
		public Node Node { get; }

		public Link Link { get; set; }
		public List<ReferenceItem> SubItems { get; } = new List<ReferenceItem>();
		public ReferenceItem Parent { get; set; }

		public Brush ItemTextBrush() => ItemService.ItemTextBrush();
		public Brush ItemTextHiddenBrush() => ItemService.ItemTextHiddenBrush();

		public IReferenceItemService ItemService { get; }
		public string ToolTip => Node.Name.DisplayFullNameWithType;

		public void AddChild(ReferenceItem child)
		{
			child.Parent = this;
			SubItems.Add(child);
		}

		public override string ToString() => $"{Text}";


		private string GetText()
		{
			if (Parent != null && Parent.Node == Node.Parent)
			{
				return Node.Name.DisplayName;
			}

			return Node.IsRoot ? "all nodes" : Node.Name.DisplayFullNoParametersName;
		}


		public void ShowCode() => ItemService.ShowCode(Node);
	}
}