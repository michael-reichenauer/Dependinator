using System;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling
{
	internal class DataNode : Equatable<DataNode>, IDataItem
	{
		public DataNode(
			NodeId id,
			NodeName name,
			string parent,
			NodeType nodeType,
			string description,
			Lazy<string> codeText,
			bool isReferenced = false)
			: this(id, name, parent, nodeType, description, codeText, RectEx.Zero, 0, null, null, isReferenced)
		{
		}

		public DataNode(NodeId id,
			NodeName name,
			string parent,
			NodeType nodeType,
			string description,
			Lazy<string> codeText,
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
			CodeText = codeText;
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
		public Lazy<string> CodeText { get; }
		public Rect Bounds { get; }
		public double ItemsScaleFactor { get; }
		public string Color { get; }
		public string ShowState { get; }

		public override string ToString() => Name.FullName;
	}
}