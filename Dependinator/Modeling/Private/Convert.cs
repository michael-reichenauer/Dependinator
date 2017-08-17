using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private
{
	internal class Convert
	{
		public static DataNode ToNode(Data.Node node)
		{
			return new DataNode(node.Name, node.Type);
		}

		public static Data.Node ToDataNode(DataNode node)
		{
			return new Data.Node
			{
				Name = node.Name,
				Type = node.NodeType,
			};
		}

		public static DataLink ToLink(Data.Link link)
		{
			return new DataLink(link.Source, link.Target);
		}


		public static Data.Link ToDataLink(DataLink link)
		{
			return new Data.Link
			{
				Source = link.Source,
				Target = link.Target,
			};
		}
	}
}