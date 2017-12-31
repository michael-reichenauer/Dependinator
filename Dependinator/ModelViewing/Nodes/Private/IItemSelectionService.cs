using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal interface IItemSelectionService
	{
		void Clicked(NodeViewModel node);
		void Clicked(LineViewModel line);
		void Clicked();
	}
}