using System.Collections.Generic;
using Dependiator.Modeling.Nodes;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal class ReflectionModel
	{
		private Dictionary<string, Data.Node> nodes { get; } =
			new Dictionary<string, Data.Node>();

		private Dictionary<string, Data.Link> links { get; } =
			new Dictionary<string, Data.Link>();


		public IReadOnlyDictionary<string, Data.Node> Nodes => nodes;
		public IReadOnlyDictionary<string, Data.Link> Links => links;

		public Data.Node AddNode(string nodeName, NodeType nodeType)
		{
			Data.Node node = new Data.Node
			{
				Name = nodeName,
				Type = nodeType
			};


			nodes[node.Name] = node;
			return node;
		}


		public void AddLink(string sourceNodeName, string targetNodeName)
		{
			Data.Link link = new Data.Link
			{
				Source = sourceNodeName,
				Target = targetNodeName
			};


			string linkName = $"{link.Source}->{link.Target}";

			links[linkName] = link;
		}
	}
}