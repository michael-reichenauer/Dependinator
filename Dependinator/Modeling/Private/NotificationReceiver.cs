using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling.Private.Serializing;


namespace Dependinator.Modeling.Private
{
	internal class NotificationReceiver : MarshalByRefObject
	{
		private readonly IModelNotifications modelNotifications;


		public NotificationReceiver(IModelNotifications modelNotifications)
		{
			this.modelNotifications = modelNotifications;
		}


		public override object InitializeLifetimeService() => null;


		public void ReceiveNodes(List<Data.Node> dataNodes)
		{
			List<DataNode> nodes = dataNodes.Select(ToNode).ToList();

			modelNotifications.UpdateNodes(nodes);
		}


		public void ReceiveLinks(List<Data.Link> dataLinks)
		{
			List<DataLink> links = dataLinks.Select(ToLink).ToList();

			modelNotifications.UpdateLinks(links);
		}


		private static DataLink ToLink(Data.Link link) => new DataLink(link.Source, link.Target);

		private static DataNode ToNode(Data.Node node) => new DataNode(node.Name, node.Type);
	}
}