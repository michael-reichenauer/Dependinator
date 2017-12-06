using System.Windows;
using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelViewing.Nodes
{
	internal class TypeViewModel : NodeViewModel
	{
		public TypeViewModel(INodeViewModelService nodeViewModelService, Node node)
			: base(nodeViewModelService, node)
		{
			ViewName = nameof(TypeView);
		}
	}
}