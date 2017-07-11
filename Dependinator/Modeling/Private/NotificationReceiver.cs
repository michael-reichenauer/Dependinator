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


		private static Node ToNode(Data.Node node)
		{
			NodeName name = node.Name;
			NodeId parentId = new NodeId(name.ParentName);

			if (node.Type == Data.NodeType.TypeType)
			{
				return new NamespaceNode(parentId, name);
			}
			else if (node.Type == Data.NodeType.MemberType)
			{
				return new MemberNode(parentId, name);
			}
			else
			{
				return new NamespaceNode(parentId, name);
			}
		}
	}
}