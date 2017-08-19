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


		public void ReceiveNodes(List<Dtos.Node> dataNodes)
		{
			List<DataNode> nodes = dataNodes.Select(Convert.ToDataNode).ToList();

			modelNotifications.UpdateNodes(nodes);
		}


		public void ReceiveLinks(List<Dtos.Link> dataLinks)
		{
			List<DataLink> links = dataLinks.Select(Convert.ToDataLink).ToList();

			modelNotifications.UpdateLinks(links);
		}
	}
}