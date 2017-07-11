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

			// Type of node, Currently one of "NameSpace", "Type", "Member"
			public string Type { get; set; }

			// Optional view data like node location and size
			public ViewData ViewData { get; set; }
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

		// Optional view data for a node like location, size and color
		[Serializable]
		public class ViewData
		{
			public double X { get; set; }
			public double Y { get; set; }
			public double Width { get; set; }
			public double Height { get; set; }
			public double Scale { get; set; }
			public double OffsetX { get; set; }
			public double OffsetY { get; set; }
			public string Color { get; set; }
		}

		internal static class NodeType
		{
			public static string NameSpaceType = "NameSpace";
			public static readonly string TypeType = "Type";
			public static readonly string MemberType = "Member";
		}
	}
}