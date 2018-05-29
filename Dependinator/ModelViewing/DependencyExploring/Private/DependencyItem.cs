using System;
using System.Collections.Generic;
using Dependinator.Common;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyItem
	{
		public DependencyItem(NodeName nodeName, Lazy<string> codeText)
		{
			NodeName = nodeName;
			CodeText = codeText;
		}

		public NodeName NodeName { get; }
		public Lazy<string> CodeText { get; }

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