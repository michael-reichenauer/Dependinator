using System;
using System.Collections.Generic;

namespace Dependinator.ModelParsing
{
	public static class JsonTypes
	{
		// A model contains a list of nodes, links and lines
		[Serializable]
		public class Model
		{
			public string FormatVersion { get; set; } = "1";
			public List<Item> Items { get; set; }
		}


		[Serializable]
		public class Item
		{
			public Node Node { get; set; }
			public Link Link { get; set; }
			public Line Line { get; set; }
		}


		// A node
		[Serializable]
		public class Node
		{
			// The name of a node with '.' separating hierarchy, e.g. like in namespaces
			public string Name { get; set; }

			// Optional data like type, node location and size ...
			public string Parent { get; set; }
			public string Type { get; set; }
			public string Bounds { get; set; }
			public string Color { get; set; }
			public double ItemsScaleFactor { get; set; }
			public string ItemsOffset { get; set; }
		}


		// Link between two nodes
		[Serializable]
		public class Link
		{
			// The source node name
			public string Source { get; set; }

			// The target node name
			public string Target { get; set; }
		}


		// Line between two nodes (with list of links)
		[Serializable]
		public class Line
		{
			// The source node name
			public string Source { get; set; }

			// The target node name
			public string Target { get; set; }

			public List<string> Points { get; set; }

			public int LinkCount { get; set; }
		}


		internal static class NodeType
		{
			public static string NameSpace = "NameSpace";
			public static readonly string Type = "Type";
			public static readonly string Member = "Member";
		}
	}
}