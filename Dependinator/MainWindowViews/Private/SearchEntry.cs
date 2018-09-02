using Dependinator.ModelViewing;


namespace Dependinator.MainWindowViews.Private
{
    internal class SearchEntry
    {
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


        public string Name { get; }
        public NodeName NodeName { get; }


        public override string ToString() => Name;
    }
}
