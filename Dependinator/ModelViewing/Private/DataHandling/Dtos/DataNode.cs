using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
	internal class DataNodeName : Equatable<DataNodeName>
	{
		public static readonly DataNodeName Root = new DataNodeName("");

		public DataNodeName(string fullName)
		{
			this.FullName = fullName;

			IsEqualWhenSame(fullName);
		}


		public string FullName { get; }

		public static DataNodeName From(string fullName)
		{
			return new DataNodeName(fullName);
		}

		public override string ToString() => FullName;
	}


	internal class DataNode : Equatable<DataNode>, IDataItem
	{
		public DataNode(
			DataNodeName name,
			DataNodeName parent,
			NodeType nodeType)
		{
			Name = name;
			Parent = parent;
			NodeType = nodeType;

			IsEqualWhenSame(Name);
		}


		public DataNodeName Name { get; }
		public DataNodeName Parent { get; }
		public NodeType NodeType { get; }

		// Node properties
		public bool IsReferenced { get; set; }
		public string Description { get; set; }
		public Rect Bounds { get; set; } = RectEx.Zero;
		public double Scale { get; set; }
		public string Color { get; set; }
		public string ShowState { get; set; }


		public override string ToString() => Name.FullName;
	}
}