using System.Collections.Generic;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal class ModelViewData
	{
		public IDictionary<string, Data.ViewData> viewData { get; } =
			new Dictionary<string, Data.ViewData>();
	}
}