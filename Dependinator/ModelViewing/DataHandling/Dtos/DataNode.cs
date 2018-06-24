using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal class DataNode : Equatable<DataNode>, IDataItem
	{
		public DataNode(
			NodeId id,
			NodeName name,
			string parent,
			NodeType nodeType,
			string description,
			bool isReferenced = false)
			: this(id, name, parent, nodeType, description, RectEx.Zero, 0, null, null, isReferenced)
		{
		}

		public DataNode(
			NodeId id,
			NodeName name,
			string parent,
			NodeType nodeType,
			string description,
			Rect bounds,
			double itemsScaleFactor,
			string color,
			string showState,
			bool isReferenced = false)
		{
			Id = id;
			Name = name;
			Parent = parent;
			NodeType = nodeType;
			IsReferenced = isReferenced;
			Description = description;
			Bounds = bounds;
			ItemsScaleFactor = itemsScaleFactor;
			Color = color;
			ShowState = showState;

			IsEqualWhenSame(Name);
		}


		public NodeId Id { get; }
		public NodeName Name { get; }
		public string Parent { get; }
		public NodeType NodeType { get; }
		public bool IsReferenced { get; }
		public string Description { get; }
		public Rect Bounds { get; }
		public double ItemsScaleFactor { get; }
		public string Color { get; }
		public string ShowState { get; }

		public override string ToString() => Name.FullName;
	}
}