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


		public ModelNode SendDefinedNodex(string name, string nodeType, string group)
		{
			return SendNode(name, nodeType, group);
		}


		public ModelNode SendDefinedNode(string name, string nodeType, string group)
		{
			return SendNode(name, nodeType, group);
		}


		public ModelNode SendReferencedNode(string name, string nodeType)
		{
			return SendNode(name, nodeType, null);
		}


		private ModelNode SendNode(string name, string nodeType, string group)
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
			node = new ModelNode(name, nodeType, RectEx.Zero, 0, PointEx.Zero, null, group);

			if (name.Contains("?Axis_Themes"))
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