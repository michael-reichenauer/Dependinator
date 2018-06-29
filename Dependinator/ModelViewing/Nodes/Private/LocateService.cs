using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class LocateService : ILocateService
	{
		private readonly IModelService modelService;

		private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(5);
		private static readonly int MoveSteps = 10;


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

		

				operation.nodes = node.AncestorsAndSelf()
					.Where(n => !n.IsRoot).Reverse()
					.ToList();
				operation.TargetNode = operation.nodes.First();

				operation.Timer = new DispatcherTimer(
					StepInterval,
					DispatcherPriority.Normal,
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
			Node targetNode = operation.TargetNode;
			double itemScale = targetNode.View.ViewModel.ItemScale;

			double rootScale = targetNode.Root.View.ItemsCanvas.Scale;
			if (targetNode.Parent.IsRoot && rootScale > 2)
			{
				operation.Zoom = Math.Max(0.90, 2 / rootScale);
				//Log.Debug($"Zoom out {operation.Zoom.TS()}");
				return;
			}


			Rect ancestorArea = targetNode.View.ViewModel.ItemBounds;
			Point ancestorCenter = new Point(
				ancestorArea.Left + ancestorArea.Width / 2,
				ancestorArea.Top + ancestorArea.Height / 2);
			Point ancestorScreenCenter2 = targetNode.View.ViewModel.ItemOwnerCanvas.CanvasToScreenPoint2(ancestorCenter);

			Rect ancestorArea2 = operation.nodes.Last().View.ViewModel.ItemBounds;
			Point ancestorCenter2 = new Point(
				ancestorArea2.Left + ancestorArea2.Width / 2,
				ancestorArea2.Top + ancestorArea2.Height / 2);
			Point t = operation.nodes.Last().View.ViewModel.ItemOwnerCanvas.CanvasToScreenPoint2(ancestorCenter2);




			//Log.Debug($"{targetNode}:{ancestorScreenCenter2.TS()}, {t.TS()}");

			Point screenPoint = t;
			Vector vector = operation.rootScreenCenter - screenPoint;

			if (vector.Length > MoveSteps)
			{
				vector = vector * (MoveSteps / vector.Length);
			}

			operation.Zoom = 1;
			operation.ScreenPoint = operation.rootScreenCenter - vector;

			if (Math.Abs(vector.Length) < 0.001)
			{
				if (Math.Abs(itemScale - 2) > 0.1 && itemScale < 2)
				{
					operation.Zoom = Math.Min(1.10, 2 / itemScale);
					return;
				}

				operation.index++;

				if (operation.index < operation.nodes.Count)
				{
					operation.TargetNode = operation.nodes[operation.index];
				}
				else
				{
					operation.IsDone = true;
					return;
				}
			}
		}




		private class StepsOperation
		{
			public IReadOnlyList<Node> nodes { get; set; }
			public Node TargetNode { get; set; }
			public Point rootScreenCenter;
			public ItemsCanvas rootCanvas;
			public Point zoomCenter;
			public int index { get; set; }
			public DispatcherTimer Timer { get; set; }
			public Point ScreenPoint { get; set; }
			public double Zoom { get; set; }
			public bool IsDone { get; set; }

		}

	}


}
