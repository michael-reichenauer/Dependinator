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
			List<DataNode> nodes = dataNodes.Select(Convert.ToNode).ToList();

			modelNotifications.UpdateNodes(nodes);
		}


		public void ReceiveLinks(List<Data.Link> dataLinks)
		{
			List<DataLink> links = dataLinks.Select(Convert.ToLink).ToList();

			modelNotifications.UpdateLinks(links);
		}
	}
}