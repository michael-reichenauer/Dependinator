using System.Collections.Generic;
using Dependinator.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class Sender
	{
		private readonly ModelItemsCallback callback;

		private readonly Dictionary<string, ModelNode> sentNodes = new Dictionary<string, ModelNode>();

		public int NodesCount => sentNodes.Count;


		public Sender(ModelItemsCallback modelItemsCallback)
		{
			callback = modelItemsCallback;
		}


		public void SendNode(ModelNode node)
		{
			if (node.Name.Contains("RuntimeTypeModel"))
			{

			}

			if (sentNodes.ContainsKey(node.Name))
			{
				// Already sent this node
				return;
			}


			sentNodes[node.Name] = node;

			if (node.Name.Contains("<") || node.Name.Contains(">"))
			{
				Log.Warn($"Send node: {node.Name}      {node.NodeType}");
			}

			callback(node);
		}


		public void SendLink(string sourceNodeName, string targetNodeName, NodeType targetType)
		{
			ModelLink link = new ModelLink(sourceNodeName, targetNodeName, targetType);

			callback(link);
		}
	}
}