using System.Windows;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItemsService
	{
		void Move(NodeOld node, Vector viewOffset);

		void Zoom(double zoom, Point viewPosition);
	}
}