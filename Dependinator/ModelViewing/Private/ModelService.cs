using System.Windows;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class ModelService : IModelService
	{
		private readonly IRootModelService rootModelService;

		public ModelService(IRootModelService rootModelService)
		{
			this.rootModelService = rootModelService;
		}

		public void Move(Node node, Vector viewOffset)
		{
			if (node != null)
			{
				node.MoveItems(viewOffset);
			}
			else
			{
				rootModelService.Move(viewOffset);
			}
		}

		public void Zoom(double zoom, Point viewPosition) => rootModelService.Zoom(zoom, viewPosition);
	}
}