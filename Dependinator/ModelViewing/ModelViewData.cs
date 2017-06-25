using System.Collections.Generic;
using Dependinator.ModelViewing.Modeling.Serializing;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing
{
	internal class ModelViewData
	{
		public IDictionary<NodeName, Data.ViewData> viewData { get; } =
			new Dictionary<NodeName, Data.ViewData>();
	}
}