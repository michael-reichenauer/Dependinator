using System.Collections.Generic;
using Dependinator.ModelViewing.DataHandling.Dtos;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyItem
	{
		public DependencyItem(NodeName nodeName, bool hasCode)
		{
			NodeName = nodeName;
			HasCode = hasCode;
		}

		public NodeName NodeName { get; }
		public bool HasCode { get; }

		public List<DependencyItem> SubItems { get; } = new List<DependencyItem>();
		public DependencyItem Parent { get; set; }

		public void AddChild(DependencyItem child)
		{
			child.Parent = this;
			SubItems.Add(child);
		}

		public override string ToString() => $"{NodeName}";
	}
}