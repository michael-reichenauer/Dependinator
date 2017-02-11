using System.Collections.Generic;
using System.Windows;


namespace Dependiator.Modeling.Serializing
{
	internal static class Data
	{
		// A model contains a list of nodes
		internal class Model
		{
			public List<Node> Nodes { get; set; }
		}

		// A node
		public class Node
		{
			//public static string NameSpaceType = "NameSpace";
			//public static readonly string TypeType = "Type";
			//public static readonly string MemberType = "Member";

				// The name of a node with '.' separating hierarchy like in namespaces
			public string Name { get; set; }

			// Type of node, Currently one of "NameSpace", "Type", "Member"
			public string Type { get; set; }

			// List of child nodes (if any)
			public List<Node> Nodes { get; set; }

			// List of links (if any) 
			public List<Link> Links { get; set; }

			// Optional view data like node location and size
			public ViewData ViewData { get; set; }
		}

		// Link between two nodes
		public class Link
		{
			// The target node name of the link
			public string Target { get; set; }

			// The source node name (not needed if link specified outside source node)
			public string Source { get; set; }
		}

		// Optional view data for a node like location, size and color
		public class ViewData
		{
			public double X { get; set; }
			public double Y { get; set; }
			public double Width { get; set; }
			public double Height { get; set; }
		}
	}
}