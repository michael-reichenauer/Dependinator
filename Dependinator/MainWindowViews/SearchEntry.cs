using Dependinator.ModelViewing.Nodes;


namespace Dependinator.MainWindowViews
{
	internal class SearchEntry
	{
		public string Name { get; }
		public NodeName NodeName { get; }


		public SearchEntry(string name, NodeName nodeName)
		{
			int parametersIndex = name.IndexOf('(');

			if (parametersIndex > -1)
			{
				name = name.Substring(0, parametersIndex) + "()";
			}

			Name = name;
			NodeName = nodeName;
		}


		public override string ToString() => Name;
	}
}