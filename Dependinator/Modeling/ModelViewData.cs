using System.Collections.Generic;
using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling
{
	internal class ModelViewData
	{
		public IDictionary<NodeName, Data.ViewData> viewData { get; } =
			new Dictionary<NodeName, Data.ViewData>();
	}
}