using System.Collections.Generic;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.Modeling
{
	internal class ModelViewDataOld
	{
		public IDictionary<NodeName, Data.ViewData> viewData { get; } =
			new Dictionary<NodeName, Data.ViewData>();
	}
}