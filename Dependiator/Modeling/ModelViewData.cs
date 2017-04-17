using System.Collections.Generic;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ModelViewData
	{
		public IDictionary<NodeName, Data.ViewData> viewData { get; } =
			new Dictionary<NodeName, Data.ViewData>();
	}
}