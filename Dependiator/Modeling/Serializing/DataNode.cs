using System.Collections.Generic;
using System.Windows;


namespace Dependiator.Modeling.Serializing
{
	public class DataNode
	{
		public static string NameSpaceType = "NameSpace";
		public static readonly string TypeType = "Type";
		public static readonly string MemberType = "Member";

		public string Type { get; set; }

		public string Name { get; set; }

		public List<DataNode> Nodes { get; set; }

		public List<DataLink> Links { get; set; }

		public Point? Location { get; set; }

		public Size? Size { get; set; }

		public override string ToString() => $"{Name} ({Type})";
	}
}