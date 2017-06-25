using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Serializing;


namespace Dependinator.ModelViewing
{
	internal class ModelViewData
	{
		public IDictionary<NodeName, Data.ViewData> viewData { get; } =
			new Dictionary<NodeName, Data.ViewData>();
	}
}