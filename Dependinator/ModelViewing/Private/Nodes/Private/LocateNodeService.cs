using System;
using System.Windows;
using System.Windows.Threading;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.ModelHandling.Private;


namespace Dependinator.ModelViewing.Private.Nodes.Private
{
	internal class LocateNodeService : ILocateNodeService
	{
		private readonly IModelService modelService;

		private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(10);
		private static readonly int MoveSteps = 20;
		private static readonly double ScaleAim = 2.0;
		private static readonly int MaxDistanceForZoomOut = 1000;
		private static readonly double ZoomOutFactor = 0.85;
		private static readonly double ZoomInFactor = 1.15;


		public LocateNodeService(IModelService modelService)
		{
			this.modelService = modelService;
		}


		public void StartMoveToNode(NodeName nodeName)
		{
			if (modelService.TryGetNode(nodeName, out Node node))
			{
				Operation operation = new Operation(
					node,
					node.Root.View.ItemsCanvas,
					GetRootScreenCenter(node.Root));

				// Starting a move in several small steps on the UI threads
				operation.Timer = new DispatcherTimer(
					StepInterval,
					DispatcherPriority.Normal,
					(s, e) => DoZoomAndMoveSteps(operation),
					Dispatcher.CurrentDispatcher);

				operation.Timer.Start();
			}
		}


		private static void DoZoomAndMoveSteps(Operation operation)
		{
			double targetScale = operation.TargetNode.View.ViewModel.ItemScale;
			Vector targetVector = GetTargetVector(operation);

			if (IsZoomingOut(operation, targetScale, targetVector, out double zoom))
			{
				ZoomCanvas(operation, zoom);
			}
			else if (IsMovingCanvas(targetVector, out Vector moveVector))
			{
				MoveCanvas(operation, moveVector);
			}
			else if (IsZoomingIn(targetScale, out zoom))
			{
				ZoomCanvas(operation, zoom);
			}
			else
			{
				// Reached target, stopping operation
				operation.Timer?.Stop();
			}
		}


		private static bool IsZoomingOut(
			Operation operation, double itemScale, Vector targetVector, out double zoom)
		{
			if (!operation.IsZoomOutPhase)
			{
				// Only zooming out when starting operation
				zoom = 0;
				return false;
			}

			double rootScale = operation.TargetNode.Root.View.ItemsCanvas.Scale;

			// Zooming out until we reach root level or zoomed enough the to see the
			// target node at target level and withing close distance
			if (rootScale > ScaleAim - 0.5
					&& (itemScale > ScaleAim - 0.5 || targetVector.Length > MaxDistanceForZoomOut))
			{
				zoom = Math.Max(ZoomOutFactor, (ScaleAim - 0.5) / rootScale);
				return true;
			}
			else
			{
				// Reached highest needed zoom out level, now starts the move and zoom in phase  
				operation.IsZoomOutPhase = false;
				zoom = 0;
				return false;
			}
		}


		private static bool IsMovingCanvas(Vector targetVector, out Vector moveVector)
		{
			moveVector = targetVector;

			if (Math.Abs(targetVector.Length) < 0.001)
			{
				// Reached target node close enough
				return false;
			}

			if (targetVector.Length > MoveSteps)
			{
				// Shorten the move vector, to move in several small steps
				moveVector = targetVector * (MoveSteps / targetVector.Length);
			}

			return true;
		}


		private static bool IsZoomingIn(double itemScale, out double zoom)
		{
			if (!(Math.Abs(itemScale - ScaleAim) >= 0.1) || !(itemScale < ScaleAim))
			{
				// Reached target zoom in level
				zoom = 0;
				return false;
			}

			zoom = Math.Min(ZoomInFactor, ScaleAim / itemScale);
			return true;
		}


		private static Vector GetTargetVector(Operation operation)
		{
			Point targetScreenPoint = GetCurrentTargetScreenPoint(operation);
			Vector vector = operation.RootScreenCenter - targetScreenPoint;
			return vector;
		}


		private static void MoveCanvas(Operation operation, Vector vector)
		{
			Point screenPoint = operation.RootScreenCenter - vector;
			operation.RootCanvas.MoveAllItems(screenPoint, operation.RootScreenCenter);
		}


		private static void ZoomCanvas(Operation operation, double zoom)
		{
			operation.RootCanvas.ZoomWindowCenter(zoom);
		}


		private static Point GetRootScreenCenter(Node rootNode)
		{
			// Returns the center of the root screen
			ItemsCanvas rootCanvas = rootNode.View.ItemsCanvas;
			Rect rootArea = rootCanvas.ItemsCanvasBounds;
			Point rootCenter = new Point(
				rootArea.Left + rootArea.Width / 2,
				rootArea.Top + rootArea.Height / 2);

			return rootCanvas.CanvasToScreenPoint(rootCenter);
		}


		private static Point GetCurrentTargetScreenPoint(Operation operation)
		{
			// Returns the center screen point of the target node
			Rect targetArea = operation.TargetNode.View.ViewModel.ItemBounds;
			Point targetCenter = new Point(
				targetArea.Left + targetArea.Width / 2, targetArea.Top + targetArea.Height / 2);
			return operation.TargetNode.View.ViewModel.ItemOwnerCanvas.CanvasToScreenPoint2(targetCenter);
		}


		// Contains the data needed for a zoom/move operation in progress
		private class Operation
		{
			public Node TargetNode { get; }
			public Point RootScreenCenter { get; }
			public ItemsCanvas RootCanvas { get; }

			public bool IsZoomOutPhase { get; set; } = true;
			public DispatcherTimer Timer { get; set; }


			public Operation(
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
