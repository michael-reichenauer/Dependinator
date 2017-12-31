using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes.Private
{
	[SingleInstance]
	internal class ItemSelectionService : IItemSelectionService
	{
		private NodeViewModel selectedNode;


		public void Clicked(NodeViewModel clickedNode)
		{
			if (clickedNode == selectedNode)
			{
				// User clicked om selected node
				return;
			}
			
			if (selectedNode == null && clickedNode != null)
			{
				// No node was selected and now clicked node is selected
				selectedNode = clickedNode;
				selectedNode.IsSelected = true;
				return;
			}

			if (selectedNode != null && clickedNode == null)
			{
				// User clicked on root node (deselect)
				selectedNode.IsSelected = false;
				selectedNode = null;
				return;
			}

			if (selectedNode.Node.Ancestors().Any(ancestor => clickedNode == ancestor.View.ViewModel))
			{
				// Deselect all nodes (root was clicked or some ancestor)
				selectedNode.IsSelected = false;
				selectedNode = null;
				return;
			}

			// Switch selected node
			selectedNode.IsSelected = false;
			selectedNode = clickedNode;
			selectedNode.IsSelected = true;
		}
	}
}