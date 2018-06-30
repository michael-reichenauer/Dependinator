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

		private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(10);
		private static readonly int MoveSteps = 20;
		private static readonly double ScaleAim = 2.0;
		private static readonly int MaxDistanceForZoomOut = 1000;
		private static readonly double ZoomOutFactor = 0.85;
		private static readonly double ZoomInFactor = 1.10;


		public LocateService(IModelService modelService)
		{
			this.modelService = modelService;
		}


		public void TryLocateNode(NodeId nodeId)
		{
			if (modelService.TryGetNode(nodeId, out Node node))
			{
				StepsOperation operation = new StepsOperation(
					node,
					node.Root.View.ItemsCanvas,
					GetRootScreenCenter(node));

				operation.Timer = new DispatcherTimer(
					StepInterval,
					DispatcherPriority.Normal,
					(s, e) => DoStep(operation),
					Dispatcher.CurrentDispatcher);

				operation.Timer.Start();
			}
		}


		private static void DoStep(StepsOperation operation)
		{
			StepType stepType = CalculateNextStep(operation);

			if (stepType == StepType.ZoomOut || stepType == StepType.ZoomIn)
			{
				operation.RootCanvas.ZoomWindowCenter(operation.Zoom);
			}
			else if (stepType == StepType.Move)
			{
				operation.RootCanvas.MoveAllItems(operation.ScreenPoint, operation.RootScreenCenter);
			}
			else 
			{
				operation.Timer?.Stop();
			}
		}


		private static StepType CalculateNextStep(StepsOperation operation)
		{
			Point targetScreenPoint = GetTargetScreenPoint(operation);
			Vector vector = operation.RootScreenCenter - targetScreenPoint;

			if (IsZoomingOut(operation))
			{
				return StepType.ZoomOut;
			}

			if (operation.IsZoomOutPhase)
			{
				double rootScale = operation.TargetNode.Root.View.ItemsCanvas.Scale;
				double itemScale = operation.TargetNode.View.ViewModel.ItemScale;

				if (rootScale > ScaleAim && (itemScale > ScaleAim - 0.5 || vector.Length > MaxDistanceForZoomOut))
				{
					operation.Zoom = Math.Max(ZoomOutFactor, ScaleAim / rootScale);
					return StepType.ZoomOut;
				}
				else
				{
					operation.IsZoomOutPhase = false;
				}
			}

			if (vector.Length > MoveSteps)
			{
				vector = vector * (MoveSteps / vector.Length);
			}


			if (Math.Abs(vector.Length) < 0.001)
			{
				double itemScale = operation.TargetNode.View.ViewModel.ItemScale;

				if (Math.Abs(itemScale - ScaleAim) < 0.1)
				{
					return StepType.Done;
				}

				if (itemScale < ScaleAim)
				{
					operation.Zoom = Math.Min(ZoomInFactor, ScaleAim / itemScale);
					return StepType.ZoomIn;
				}
			}

			operation.ScreenPoint = operation.RootScreenCenter - vector;
			return StepType.Move;
		}


		private static bool IsZoomingOut(StepsOperation operation)
		{
			if (operation.IsZoomOutPhase)
			{
				Point targetScreenPoint = GetTargetScreenPoint(operation);
				Vector vector = operation.RootScreenCenter - targetScreenPoint;
				double rootScale = operation.TargetNode.Root.View.ItemsCanvas.Scale;
				double itemScale = operation.TargetNode.View.ViewModel.ItemScale;

				if (rootScale > ScaleAim  - 0.5
				    && (itemScale > ScaleAim - 0.5 || vector.Length > MaxDistanceForZoomOut))
				{
					operation.Zoom = Math.Max(ZoomOutFactor, ScaleAim / rootScale);
					return true;
				}
				else
				{
					operation.IsZoomOutPhase = false;
				}
			}

			return false;
		}


		private static Point GetRootScreenCenter(Node node)
		{
			ItemsCanvas rootCanvas = node.Root.View.ItemsCanvas;
			Rect rootArea = rootCanvas.ItemsCanvasBounds;
			Point rootCenter = new Point(
				rootArea.Left + rootArea.Width / 2,
				rootArea.Top + rootArea.Height / 2);

			return rootCanvas.CanvasToScreenPoint(rootCenter);
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

		private enum StepType
		{
			Done,
			ZoomOut,
			Move,
			ZoomIn,
		}

		private class StepsOperation
		{
			public Node TargetNode { get; }
			public Point RootScreenCenter { get; }
			public ItemsCanvas RootCanvas { get; }

			public bool IsZoomOutPhase { get; set; } = true;
			public DispatcherTimer Timer { get; set; }
			public Point ScreenPoint { get; set; }
			public double Zoom { get; set; }


			public StepsOperation(
				Node targetNode,
				ItemsCanvas rootCanvas,
				Point rootScreenCenter)
			{
				RootScreenCenter = rootScreenCenter;
				TargetNode = targetNode;
				RootCanvas = rootCanvas;
			}

		}
	}
}
