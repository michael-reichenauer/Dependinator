using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyItem
	{
		public DependencyItem(Node node)
		{
			Node = node;
		}

		public Node Node { get; }

		public List<DependencyItem> SubItems { get; } = new List<DependencyItem>();
		public DependencyItem Parent { get; set; }

		public void AddChild(DependencyItem child)
		{
			child.Parent = this;
			SubItems.Add(child);
		}

		public override string ToString() => $"{Node}";
	}
}