using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class Sender
	{
		private readonly ModelItemsCallback callback;
		private Dictionary<string, ModelNode> sentNodes = new Dictionary<string, ModelNode>();


		public int NodesCount => sentNodes.Count;
		public int LinkCount { get; private set; } = 0;


		public Sender(ModelItemsCallback modelItemsCallback)
		{
			this.callback = modelItemsCallback;
		}


		public ModelNode SendNode(string name, string nodeType)
		{
			if (Util.IsCompilerGenerated(name))
			{
				Log.Warn($"Compiler generated node: {name}");
			}

			if (sentNodes.TryGetValue(name, out ModelNode node))
			{
				// Already sent this node
				return node;
			}

			node = new ModelNode(new NodeName(name), nodeType, RectEx.Zero, 0, PointEx.Zero, null);

			sentNodes[name] = node;

			//Log.Debug($"Send node: {name} {node.Type}");

			if (name == "TEdge")
			{
				
			}

			if (name.Contains("<") || name.Contains(">"))
			{
				Log.Warn($"Send node: {name}      {nodeType}");
			}

			callback(new ModelItem(node, null));
			return node;
		}


		public void SendLink(NodeName sourceNodeName, string targetNodeName)
		{
			if (Util.IsCompilerGenerated(sourceNodeName.FullName)
			    || Util.IsCompilerGenerated(targetNodeName))
			{
				Log.Warn($"Compiler generated link: {sourceNodeName}->{targetNodeName}");
			}

			if (sourceNodeName.FullName == targetNodeName)
			{
				// Skipping link to self
				return;
			}

			if (sourceNodeName.FullName.Contains("<") || sourceNodeName.FullName.Contains(">"))
			{
				Log.Warn($"Send link source: {sourceNodeName}");
			}

			if (targetNodeName.Contains("<") || targetNodeName.Contains(">"))
			{
				Log.Warn($"Send link target: {targetNodeName}");
			}

			ModelLink link = new ModelLink(sourceNodeName, new NodeName(targetNodeName));

			LinkCount++;

			//Log.Debug($"Send link: {link.Source} {link.Target}");
			callback(new ModelItem(null, link));
		}
	}
}