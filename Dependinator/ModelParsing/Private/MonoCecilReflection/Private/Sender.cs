using System.Collections.Generic;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class Sender
	{
		private readonly ModelItemsCallback callback;

		private readonly Dictionary<string, ModelNode> sentNodes = new Dictionary<string, ModelNode>();

		public int NodesCount => sentNodes.Count;
		public int LinkCount { get; private set; }


		public Sender(ModelItemsCallback modelItemsCallback)
		{
			callback = modelItemsCallback;
		}


		public ModelNode SendNode(string nodeName, string parentName, string nodeType)
		{
			if (Name.IsCompilerGenerated(nodeName))
			{
				Log.Warn($"Compiler generated node: {nodeName}");
			}

			if (sentNodes.TryGetValue(nodeName, out ModelNode node))
			{
				// Already sent this node
				return node;
			}

			//rootGroup = null;
			node = new ModelNode(nodeName, parentName, nodeType);

			sentNodes[nodeName] = node;

			//Log.Debug($"Send node: {name} {node.Type}");

			if (nodeName.Contains("<") || nodeName.Contains(">"))
			{
				Log.Warn($"Send node: {nodeName}      {nodeType}");
			}

			callback(node);
			return node;
		}


		public void SendLink(string sourceNodeName, string targetNodeName, string targetType)
		{
			if (Name.IsCompilerGenerated(sourceNodeName)
					|| Name.IsCompilerGenerated(targetNodeName))
			{
				Log.Warn($"Compiler generated link: {sourceNodeName}->{targetNodeName}");
			}

			if (sourceNodeName == targetNodeName)
			{
				// Skipping link to self
				return;
			}

			if (sourceNodeName.Contains("<") || sourceNodeName.Contains(">"))
			{
				Log.Warn($"Send link source: {sourceNodeName}");
			}

			if (targetNodeName.Contains("<") || targetNodeName.Contains(">"))
			{
				Log.Warn($"Send link target: {targetNodeName}");
			}

			ModelLink link = new ModelLink(sourceNodeName, targetNodeName, targetType);

			LinkCount++;

			//Log.Debug($"Send link: {link.Source} {link.Target}");
			callback(link);
		}
	}
}