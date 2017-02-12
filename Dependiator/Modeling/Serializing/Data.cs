using System.Collections.Generic;
using System.Windows;


namespace Dependiator.Modeling.Serializing
{
	////internal static class ImportData
	////{
	////	// A model contains a list of nodes
	////	internal class Model
	////	{
	////		public List<Node> Nodes { get; set; }
	////		public List<Link> Links { get; set; }
	////	}

	////	// A node
	////	public class Node
	////	{
	////		// The name of a node with '.' separating hierarchy, e.g. like in namespaces
	////		public string Name { get; set; }

	////		// Type of node, Currently one of "NameSpace", "Type", "Member"
	////		public string Type { get; set; }
	////	}


	////	// Link between two nodes
	////	public class Link
	////	{
	////		// The source node name
	////		public string Source { get; set; }

	////		// The target node name of the link
	////		public string Target { get; set; }
	////	}
	////}


	internal static class Data
	{
		// A model contains a list of nodes
		internal class Model
		{
			public List<Node> Nodes { get; set; }
			public List<Link> Links { get; set; }
		}

		// A node
		public class Node
		{
			// The name of a node with '.' separating hierarchy, e.g. like in namespaces
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
			// The source node name (not needed if link specified outside source node)
			public string Source { get; set; }

			// The target node name of the link
			public string Target { get; set; }
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