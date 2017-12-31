using System.Linq;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes.Private
{
	[SingleInstance]
	internal class ItemSelectionService : IItemSelectionService
	{
		private NodeViewModel selectedNode;
		private LineViewModel selectedLine;


		public void Clicked()
		{
			// User clicked on root node (deselect)
			if (IsNoSelection())
			{
				return;
			}
			
			Deselect();
		}


		public void Clicked(NodeViewModel clickedNode)
		{
			if (clickedNode == selectedNode)
			{
				// User clicked om selected node
				Deselect();
				return;
			}
			
			if (selectedNode != null && 
			    selectedNode.Node.Ancestors().Any(ancestor => clickedNode == ancestor.View.ViewModel))
			{
				// Deselect all (root was clicked or some ancestor)
				Deselect();
				return;
			}

			Deselect();

			// Selected node
			selectedNode = clickedNode;
			selectedNode.IsSelected = true;
		}


		public void Clicked(LineViewModel clickedLine)
		{
			if (clickedLine == selectedLine)
			{
				// User clicked om selected line
				Deselect();
				return;
			}


			Deselect();

			selectedLine = clickedLine;
			selectedLine.IsSelected = true;
		}


		private bool IsNoSelection()
		{
			return selectedNode == null && selectedLine == null;
		}


		private void Deselect()
		{
			if (selectedNode != null)
			{
				selectedNode.IsSelected = false;
				selectedNode = null;
			}

			if (selectedLine != null)
			{
				selectedLine.IsSelected = false;
				selectedLine = null;
			}
		}
	}
}