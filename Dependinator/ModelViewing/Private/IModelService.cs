using System.Windows;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal interface IModelService
	{
		void Move(Node node, Vector viewOffset);

		void Zoom(double zoom, Point viewPosition);
	}
}