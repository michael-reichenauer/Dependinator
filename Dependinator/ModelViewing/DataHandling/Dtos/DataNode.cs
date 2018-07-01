using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal class DataNodeName : Equatable<DataNodeName>
	{
		public DataNodeName(string fullName)
		{
			if (fullName == null)
			{

			}
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


	internal enum DataNodeType
	{
		None,
		Solution,
		Project,
		Dll,
		Exe,
		NameSpace,
		Type,
		Member,
	}



	internal class DataNode : Equatable<DataNode>, IDataItem
	{
		public DataNode(
			DataNodeName name,
			DataNodeName parent,
			DataNodeType nodeType,
			bool isReferenced)
		{
			Name = name;
			Parent = parent;
			NodeType = nodeType;
			IsReferenced = isReferenced;

			IsEqualWhenSame(Name);
		}


		public DataNodeName Name { get; }
		public DataNodeName Parent { get; }
		public DataNodeType NodeType { get; }
		public bool IsReferenced { get; }

		// Node properties
		public string Description { get; set; }
		public Rect Bounds { get; set; } = RectEx.Zero;
		public double ItemsScaleFactor { get; set; }
		public string Color { get; set; }
		public string ShowState { get; set; }

		public override string ToString() => Name.FullName;
	}
}