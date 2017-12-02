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
		public int LinkCount { get; private set; }


		public Sender(ModelItemsCallback modelItemsCallback)
		{
			callback = modelItemsCallback;
		}


		public void SendNode(ModelNode node)
		{
			//if (node.Name.Contains("IWshRuntime"))
			//{
				
			//}

			if (Name.IsCompilerGenerated(node.Name))
			{
				Log.Warn($"Compiler generated node: {node.Name}");
			}

			if (sentNodes.ContainsKey(node.Name))
			{
				// Already sent this node
				return;
			}


			sentNodes[node.Name] = node;

			//Log.Debug($"Send node: {name} {node.Type}");

			if (node.Name.Contains("<") || node.Name.Contains(">"))
			{
				Log.Warn($"Send node: {node.Name}      {node.NodeType}");
			}

			callback(node);
		}


		public void SendLink(string sourceNodeName, string targetNodeName, NodeType targetType)
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