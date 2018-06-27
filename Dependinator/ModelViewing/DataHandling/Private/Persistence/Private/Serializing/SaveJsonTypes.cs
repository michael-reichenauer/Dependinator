using System;
using System.Collections.Generic;


namespace Dependinator.ModelViewing.DataHandling.Private.Persistence.Private.Serializing
{
	public static class SaveJsonTypes
	{
		public static string Version = "1";

		// A model contains a list of nodes, links and lines
		[Serializable]
		public class Model
		{
			public string FormatVersion { get; set; } = Version;
			public List<Item> Items { get; set; }
		}


		[Serializable]
		public class Item
		{
			public Node Node { get; set; }
		}


		// A node
		[Serializable]
		public class Node
		{
			// The name of a node with '.' separating hierarchy, e.g. like in namespaces
			public string Id { get; set; }
			public string Bounds { get; set; }
			public string Color { get; set; }
			public double Scale { get; set; }
			public string State { get; set; }
		}
	}
}