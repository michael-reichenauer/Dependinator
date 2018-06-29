using System;
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

		private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(5);
		private static readonly int MoveSteps = 20;
		private static readonly double ScaleAim = 2.0;


		public LocateService(IModelService modelService)
		{
			this.modelService = modelService;
		}


		public void TryLocateNode(NodeId nodeId)
		{
			if (modelService.TryGetNode(nodeId, out Node node))
			{
				StepsOperation operation = new StepsOperation();
				operation.TargetNode = node;

				operation.rootCanvas = node.Root.View.ItemsCanvas;

				Rect rootArea = operation.rootCanvas.ItemsCanvasBounds;
				Point rootCenter = new Point(
					rootArea.Left + rootArea.Width / 2,
					rootArea.Top + rootArea.Height / 2);
				operation.rootScreenCenter = operation.rootCanvas.CanvasToScreenPoint(rootCenter);

				operation.Timer = new DispatcherTimer(
					StepInterval,
					DispatcherPriority.Render,
					(s, e) => DoStep(operation),
					Dispatcher.CurrentDispatcher);

				operation.Timer.Start();
			}
		}


		private void DoStep(StepsOperation operation)
		{
			CalculateNextStep(operation);
			if (operation.IsDone)
			{
				operation.Timer?.Stop();
				return;
			}

			if (operation.Zoom != 1)
			{
				operation.rootCanvas.ZoomWindowCenter(operation.Zoom);
			}
			else
			{
				operation.rootCanvas.MoveAllItems(operation.ScreenPoint, operation.rootScreenCenter);
			}
		}



		private static void CalculateNextStep(StepsOperation operation)
		{
			Point targetScreenPoint = GetTargetScreenPoint(operation);
			Vector vector = operation.rootScreenCenter - targetScreenPoint;
			Log.Debug($"Dist {vector.Length}");

			if (!operation.IsZoomInPhase)
			{
				double rootScale = operation.TargetNode.Root.View.ItemsCanvas.Scale;
				double itemScale = operation.TargetNode.View.ViewModel.ItemScale;
				Log.Debug($"{rootScale.TS()} {itemScale.TS()}");
				if (rootScale > ScaleAim && (itemScale > ScaleAim || vector.Length > 1000))
				{
					Log.Debug($"Zoom out, {rootScale.TS()} {itemScale.TS()}, {vector.Length.TS()} ");
					operation.Zoom = Math.Max(0.90, ScaleAim / rootScale);
					return;
				}
				else
				{
					operation.IsZoomInPhase = true;
				}
			}

			
			if (vector.Length > MoveSteps)
			{
				vector = vector * (MoveSteps / vector.Length);
			}

			Log.Debug($"Move {vector.Length}");

			operation.Zoom = 1;
			operation.ScreenPoint = operation.rootScreenCenter - vector;

			if (Math.Abs(vector.Length) < 0.001)
			{
				double itemScale = operation.TargetNode.View.ViewModel.ItemScale;

				if (Math.Abs(itemScale - ScaleAim) < 0.1)
				{
					operation.IsDone = true;
					return;
				}

				if (itemScale < ScaleAim)
				{
					operation.Zoom = Math.Min(1.08, ScaleAim / itemScale);
					return;
				}
			}
		}


		private static Point GetTargetScreenPoint(StepsOperation operation)
		{
			Rect targetArea = operation.TargetNode.View.ViewModel.ItemBounds;
			Point targetCenter = new Point(
				targetArea.Left + targetArea.Width / 2,
				targetArea.Top + targetArea.Height / 2);
			Point targetScreenPoint = operation.TargetNode.View.ViewModel.ItemOwnerCanvas.CanvasToScreenPoint2(targetCenter);
			return targetScreenPoint;
		}


		private class StepsOperation
		{
			public Node TargetNode { get; set; }
			public Point rootScreenCenter;
			public ItemsCanvas rootCanvas;
			public DispatcherTimer Timer { get; set; }
			public Point ScreenPoint { get; set; }
			public double Zoom { get; set; }
			public bool IsDone { get; set; }
			public bool IsZoomInPhase { get; set; }
		}

	}


}
