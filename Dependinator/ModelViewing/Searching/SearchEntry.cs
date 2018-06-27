using Dependinator.ModelViewing.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Searching
{
	internal class SearchEntry
	{
		public string Name { get; set; }

		public SearchEntry(string name, NodeId nodeId)
		{
			int parametersIndex = name.IndexOf('(');

			if (parametersIndex > -1)
			{
				name = name.Substring(0, parametersIndex) + "()";
			}

			Name = name;
		}


		public override string ToString() => Name;
	}
}