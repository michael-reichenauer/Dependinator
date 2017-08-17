using System;
using System.Collections.Generic;

namespace Dependinator.Modeling.Private.Serializing
{
	public static class Data
	{
		// A model contains a list of nodes
		[Serializable]
		public class Model
		{
			public List<Node> Nodes { get; set; }
			public List<Link> Links { get; set; }
		}

		// A node
		[Serializable]
		public class Node
		{
			// The name of a node with '.' separating hierarchy, e.g. like in namespaces
			public string Name { get; set; }

			// Optional data like type, node location and size ...
			public string Type { get; set; }
			public double X { get; set; }
			public double Y { get; set; }
			public double Width { get; set; }
			public double Height { get; set; }
			public double Scale { get; set; }
			public double OffsetX { get; set; }
			public double OffsetY { get; set; }
			public string Color { get; set; }
		}


		// Link between two nodes
		[Serializable]
		public class Link
		{
			// The source node name
			public string Source { get; set; }

			// The target node name of the link
			public string Target { get; set; }
		}

		//// Optional view data for a node like location, size and color
		//[Serializable]
		//public class NodeData
		//{
			
		//}

		internal static class NodeType
		{
			public static string NameSpace = "NameSpace";
			public static readonly string Type = "Type";
			public static readonly string Member = "Member";
		}
	}
}