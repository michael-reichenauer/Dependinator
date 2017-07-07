using System.Windows;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class ModelService : IModelService
	{
		private readonly IModelViewService modelViewService;

		public ModelService(IModelViewService modelViewService)
		{
			this.modelViewService = modelViewService;
		}

		public void Move(Node node, Vector viewOffset)
		{
			if (node != null)
			{
				node.MoveItems(viewOffset);
			}
			else
			{
				modelViewService.Move(viewOffset);
			}
		}

		public void Zoom(double zoom, Point viewPosition) => modelViewService.Zoom(zoom, viewPosition);
	}
}