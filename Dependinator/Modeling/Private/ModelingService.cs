using System.Collections.Generic;
using Dependinator.Modeling.Private.Analyzing;
using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private
{
	internal class ModelingService : IModelingService
	{
		private readonly IReflectionService reflectionService;
		private readonly IDataSerializer dataSerializer;


		public ModelingService(
			IReflectionService reflectionService,
			IDataSerializer dataSerializer)
		{

			this.reflectionService = reflectionService;
			this.dataSerializer = dataSerializer;
		}


		public void Analyze(string path)
		{
			reflectionService.Analyze(path);
		}


		public void Serialize(IEnumerable<DataNode> nodes, IEnumerable<DataLink> links, string path)
		{
			dataSerializer.Serialize(nodes, links, path);
		}


		public bool TryDeserialize(string path)
		{
			return dataSerializer.TryDeserialize(path);
		}




		//private static Data.ViewData ToViewData(NodeOld node)
		//{
		//	Data.ViewData viewData = new Data.ViewData
		//	{
		//		Color = node.PersistentNodeColor,
		//		X = node.ItemBounds.X,
		//		Y = node.ItemBounds.Y,
		//		Width = node.ItemBounds.Width,
		//		Height = node.ItemBounds.Height,
		//		Scale = node.ItemsScale,
		//		OffsetX = node.ItemsOffset.X,
		//		OffsetY = node.ItemsOffset.Y
		//	};

		//	return viewData;
		//}




		//private static Rect? ToBounds(Data.ViewData viewData)
		//{
		//	if (viewData == null || viewData.Width == 0)
		//	{
		//		return null;
		//	}

		//	return new Rect(
		//		new Point(viewData.X, viewData.Y),
		//		new Size(viewData.Width, viewData.Height));
		//}


	}
}