﻿using System.Windows;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing.Private.Items
{
	internal class ItemsViewModel : ViewModel
	{
		private readonly IItemsService itemsService;
		private readonly NodeOld node;

		public ItemsViewModel(IItemsService itemsService, NodeOld node, IItemsCanvas itemsCanvas)
		{
			ItemsCanvas = itemsCanvas;
			this.itemsService = itemsService;
			this.node = node;
			ItemsCanvas = itemsCanvas;
		}

		public IItemsCanvas ItemsCanvas { get; }

		public bool IsRoot => node == null;

		public void SetZoomableCanvas(ZoomableCanvas zoomableCanvas) =>
			ItemsCanvas.SetZoomableCanvas(zoomableCanvas);

		public void MoveCanvas(Vector viewOffset) => itemsService.Move(node, viewOffset);

		public void ZoomRoot(double zoom, Point viewPosition) => itemsService.Zoom(zoom, viewPosition);

		public void Zoom(double zoom, Point viewPosition) => node.Zoom(zoom, viewPosition);

		public void SizeChanged() => ItemsCanvas.SizeChanged();
	}
}