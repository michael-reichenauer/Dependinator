using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class LocateService : ILocateService
	{
		private readonly IModelService modelService;
		private static readonly int MoveSteps = 20;


		public LocateService(IModelService modelService)
		{
			this.modelService = modelService;
		}


		public void TryLocateNode(NodeId nodeId)
		{
			if (modelService.TryGetNode(nodeId, out Node node))
			{
				StepsOperation operation = new StepsOperation();

				operation.rootCanvas = node.Root.View.ItemsCanvas;


				Rect rootArea = operation.rootCanvas.ItemsCanvasBounds;
				Point rootCenter = new Point(
					rootArea.Left + rootArea.Width / 2,
					rootArea.Top + rootArea.Height / 2);
				operation.rootScreenCenter = operation.rootCanvas.CanvasToScreenPoint(rootCenter);
				operation.zoomCenter = new Point(rootArea.Width / 2, rootArea.Height);

				operation.nodeIds = node.AncestorsAndSelf()
					.Where(n => !n.IsRoot).Reverse()
					.Select(n => n.Id)
					.ToList();


				operation.Timer = new DispatcherTimer(
					TimeSpan.FromMilliseconds(20),
					DispatcherPriority.ApplicationIdle,
					(s, e) => DoStep(operation),
					Dispatcher.CurrentDispatcher);

				operation.Timer.Start();
			}
		}


		private void DoStep(StepsOperation operation)
		{
			operation.Timer?.Stop();
			GetNextStep(operation);
			if (operation.IsDone)
			{
				return;
			}


			if (operation.Zoom != 1)
			{
				operation.rootCanvas.ZoomNode(operation.Zoom, operation.zoomCenter);
			}
			else
			{
				operation.rootCanvas.MoveAllItems(operation.ScreenPoint, operation.rootScreenCenter);
			}

			operation.Timer?.Start();
		}



		private void GetNextStep(StepsOperation operation)
		{
			if (operation.index < operation.nodeIds.Count
					&& modelService.TryGetNode(operation.nodeIds[operation.index], out Node ancestor))
			{
				//index++;
				double itemScale = ancestor.View.ViewModel.ItemScale;



				//if (Math.Abs(itemScale - 2) > 0.1 )
				//{
				//	double zoomStep = itemScale > 2 ? 0.99 : 1.01;
				//	return new Step
				//	{
				//		ScreenPoint = rootScreenCenter,
				//		Zoom = zoomStep,
				//	};
				//}

				Rect ancestorArea = ancestor.View.ViewModel.ItemBounds;
				Point ancestorCenter = new Point(
					ancestorArea.Left + ancestorArea.Width / 2,
					ancestorArea.Top + ancestorArea.Height / 2);
				Point ancestorScreenCenter = ancestor.View.ViewModel.ItemOwnerCanvas.CanvasToScreenPoint(ancestorCenter);

				//scale = scale / ancestor.View.ViewModel.ItemOwnerCanvas.ScaleFactor;
				Point screenPoint = ancestorScreenCenter;
				Vector vector = operation.rootScreenCenter - screenPoint;


				double zoomStep = itemScale > 2 ? 0.8 : 1.05;
				Log.Debug($"{ancestor} scale: {itemScale.TS()}, dist: {vector.Length.TS()}, zoomstep: {zoomStep}");

				Log.Debug($"close {Math.Abs(itemScale - 2)}");
				if (Math.Abs(itemScale - 2) > 0.1 && zoomStep < 1)
				{
					Log.Debug("Zoom out");
					operation.ScreenPoint = operation.rootScreenCenter;
					operation.Zoom = zoomStep;
					return;
				}


				if (Math.Abs(vector.Length) < 0.1)
				{
					if (Math.Abs(itemScale - 2) > 0.1)
					{
						operation.ScreenPoint = operation.rootScreenCenter;
						operation.Zoom = zoomStep;
						return;
					}

					operation.index++;

					//return null;
				}


				if (vector.Length < MoveSteps)
				{

					//index++;
				}
				else
				{
					vector = vector * (MoveSteps / vector.Length);
				}

				operation.ScreenPoint = operation.rootScreenCenter - vector;
				operation.Zoom = 1;
				return;
			}

			operation.IsDone = true;
		}




		private class StepsOperation
		{
			public List<NodeId> nodeIds;
			public Point rootScreenCenter;
			public ItemsCanvas rootCanvas;
			public Point zoomCenter;
			public int index { get; set; }
			public DispatcherTimer Timer { get; set; }
			public Point ScreenPoint { get; set; }
			public double Zoom { get; set; }
			public Point ZoomCenter { get; set; }
			public bool IsDone { get; set; }
		}

	}


}
