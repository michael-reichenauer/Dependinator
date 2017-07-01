using System.Collections.Generic;
using Dependinator.Modeling.Serializing;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.Modeling.Analyzing.Private
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
			if (targetNodeName.Contains("&"))
			{
			}

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