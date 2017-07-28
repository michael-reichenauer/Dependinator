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
			List<Node> nodes = dataNodes.Select(ToNode).ToList();

			modelNotifications.UpdateNodes(nodes);
		}


		public void ReceiveLinks(List<Data.Link> dataLinks)
		{
			List<Link> links = dataLinks.Select(ToLink).ToList();

			modelNotifications.UpdateLinks(links);
		}


		private static Link ToLink(Data.Link link)
		{
			return new Link(new NodeId(link.Source), new NodeId(link.Target));
		}


		private static Node ToNode(Data.Node node) => new Node(node.Name, node.Type);
	}
}