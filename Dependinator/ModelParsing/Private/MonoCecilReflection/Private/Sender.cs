using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class Sender
	{

		private readonly ModelItemsCallback callback;

		private readonly Dictionary<string, ModelNode> sentNodes = new Dictionary<string, ModelNode>();


		public int NodesCount => sentNodes.Count;
		public int LinkCount { get; private set; } = 0;


		public Sender(ModelItemsCallback modelItemsCallback)
		{
			this.callback = modelItemsCallback;
		}


		public ModelNode SendDefinedNode(string name, string parent, string nodeType)
		{
			return SendNodeImp(name, parent, nodeType);
		}


		public ModelNode SendReferencedNode(string name, string nodeType)
		{
			return SendNodeImp(name, null, nodeType);
		}


		private ModelNode SendNodeImp(string name, string parent, string nodeType)
		{
			if (Name.IsCompilerGenerated(name))
			{
				Log.Warn($"Compiler generated node: {name}");
			}

			if (sentNodes.TryGetValue(name, out ModelNode node))
			{
				// Already sent this node
				return node;
			}

			//rootGroup = null;
			node = new ModelNode(name, parent, nodeType, RectEx.Zero, 0, PointEx.Zero, null);

			if (name.Contains(".get_") || name.Contains(".set_"))
			{
				
			}

			sentNodes[name] = node;

			//Log.Debug($"Send node: {name} {node.Type}");

			if (name.Contains("<") || name.Contains(">"))
			{
				Log.Warn($"Send node: {name}      {nodeType}");
			}

			callback(node);
			return node;
		}


		public void SendLink(string sourceNodeName, string targetNodeName)
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

			ModelLink link = new ModelLink(sourceNodeName, targetNodeName);

			LinkCount++;

			//Log.Debug($"Send link: {link.Source} {link.Target}");
			callback(link);
		}
	}
}