using System.Collections.Generic;


namespace Dependiator.Modeling.Serializing
{
	internal class DataModel
	{
		public Dictionary<string, Data.Node> NodesByName { get; } = new Dictionary<string, Data.Node>();

		public Data.Model Model { get; set; }
	}
}