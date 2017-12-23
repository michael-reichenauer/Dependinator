using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes.Private
{
	[SingleInstance]
	internal class NodeSelectionService : INodeSelectionService
	{
		public void Clicked(Node clickedNode)
		{
			if (SelectedNode == null)
			{
				SelectedNode = clickedNode.Root;
			}

			if (clickedNode == SelectedNode)
			{
				return;
			}

			if (SelectedNode.Ancestors().Any(ancestor => clickedNode == ancestor))
			{
				// Deselect all nodes (as if root was clicked)
				clickedNode = clickedNode.Root;
			}

			SelectedNode.IsSelected = false;
			SelectedNode = clickedNode;
			SelectedNode.IsSelected = true;
		}


		public Node SelectedNode { get; private set; }
	}
}