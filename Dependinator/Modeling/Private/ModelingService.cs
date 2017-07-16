using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Modeling.Private.Analyzing;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.Modeling.Private
{
	internal class ModelingService : IModelingService
	{
		private readonly INodeService nodeService;
		private readonly ILinkService linkService;
		private readonly IReflectionService reflectionService;
		private readonly IDataSerializer dataSerializer;


		public ModelingService(
			INodeService nodeService,
			ILinkService linkService,
			IReflectionService reflectionService,
			IDataSerializer dataSerializer)
		{
			this.nodeService = nodeService;
			this.linkService = linkService;
			this.reflectionService = reflectionService;
			this.dataSerializer = dataSerializer;
		}


		public ModelOld Analyze(string path, ModelViewDataOld modelViewData)
		{
			reflectionService.Analyze(path);
			return new ModelOld(CreateRootNode());
		}


		public ModelViewDataOld ToViewData(ModelOld model)
		{
			ModelViewDataOld modelViewData = new ModelViewDataOld();

			AddViewData(model.Root, modelViewData);

			return modelViewData;
		}



		public void Serialize(ModelOld model, string path)
		{
			Data.Model dataModel = ToDataModel(model);

			dataSerializer.Serialize(dataModel, path);
		}


		public bool TryDeserialize(string path, out ModelOld model)
		{
			if (dataSerializer.TryDeserialize(path, out Data.Model dataModel))
			{
				model = ToModel(dataModel, null);
				return true;
			}

			model = null;
			return false;
		}



		private ModelOld ToModel(Data.Model dataModel, ModelViewDataOld modelViewData)
		{
			IEnumerable<Data.Node> nodes = dataModel.Nodes ?? Enumerable.Empty<Data.Node>();
			IEnumerable<Data.Link> links = dataModel.Links ?? Enumerable.Empty<Data.Link>();

			NodeOld root = CreateRootNode();
			ModelOld model = new ModelOld(root);

			Timing t = Timing.Start();
			foreach (Data.Node node in nodes)
			{
				AddNode(node, null, model, modelViewData);
			}
			t.Log("Added nodes");

			foreach (Data.Link link in links)
			{
				AddLink(link, model, modelViewData);
			}
			t.Log("Added links");

			return model;
		}


		private NodeOld CreateRootNode()
		{
			NodeOld root = new NodeOld(nodeService, linkService, null, NodeName.Root, NodeType.NameSpaceType);
			return root;
		}


		private Data.Model ToDataModel(ModelOld model)
		{
			Data.Model dataModel = new Data.Model();
			dataModel.Nodes = new List<Data.Node>();
			dataModel.Links = new List<Data.Link>();

			Queue<NodeOld> nodes = new Queue<NodeOld>();
			model.Root.ChildNodes.ForEach(nodes.Enqueue);

			while (nodes.Any())
			{
				NodeOld node = nodes.Dequeue();
				Data.Node dataNode = ToDataNode(node);
				dataModel.Nodes.Add(dataNode);

				node.ChildNodes.ForEach(nodes.Enqueue);

				DataLinksQuery(node).ForEach(dataModel.Links.Add);
			}

			return dataModel;
		}


		private static void AddViewData(NodeOld node, ModelViewDataOld modelViewData)
		{
			Data.ViewData nodeViewData = ToViewData(node);

			modelViewData.viewData[node.NodeName] = nodeViewData;

			foreach (NodeOld childNode in node.ChildNodes)
			{
				AddViewData(childNode, modelViewData);
			}
		}


		private static Data.ViewData ToViewData(NodeOld node)
		{
			Data.ViewData viewData = new Data.ViewData
			{
				Color = node.PersistentNodeColor,
				X = node.ItemBounds.X,
				Y = node.ItemBounds.Y,
				Width = node.ItemBounds.Width,
				Height = node.ItemBounds.Height,
				Scale = node.ItemsScale,
				OffsetX = node.ItemsOffset.X,
				OffsetY = node.ItemsOffset.Y
			};

			return viewData;
		}


		private void AddNode(
			Data.Node dataNode,
			NodeName parentName,
			ModelOld model,
			ModelViewDataOld modelViewData)
		{
			NodeName fullName = string.IsNullOrEmpty(parentName)
				? dataNode.Name
				: parentName + "." + dataNode.Name;

			NodeOld node = GetOrAddNode(fullName, model, modelViewData);

			node.SetType(dataNode.Type);

			TrySetViewData(dataNode.ViewData, modelViewData, node, fullName);
		}


		private static Rect? ToBounds(Data.ViewData viewData)
		{
			if (viewData == null || viewData.Width == 0)
			{
				return null;
			}

			return new Rect(
				new Point(viewData.X, viewData.Y),
				new Size(viewData.Width, viewData.Height));
		}


		private void AddLink(Data.Link dataLink, ModelOld model, ModelViewDataOld modelViewData)
		{
			NodeOld sourceNode = GetOrAddNode(dataLink.Source, model, modelViewData);
			NodeOld targetNode = GetOrAddNode(dataLink.Target, model, modelViewData);

			LinkOld link = new LinkOld(sourceNode, targetNode);
			sourceNode.Links.Add(link);
		}


		private Data.Node ToDataNode(NodeOld node)
		{
			Data.Node dataNode = new Data.Node
			{
				Name = node.NodeName,
				Type = node.NodeType,
				ViewData = ToViewData(node)
			};

			return dataNode;
		}


		private static IEnumerable<Data.Link> DataLinksQuery(NodeOld node) =>
			node.Links.Links
				.Select(l => new Data.Link { Source = node.NodeName, Target = l.Target.NodeName });


		private NodeOld GetOrAddNode(NodeName nodeName, ModelOld model, ModelViewDataOld modelViewData)
		{
			if (!model.Nodes.TryGetValue(nodeName, out NodeOld node))
			{
				node = CreateNode(nodeName, model, modelViewData);
			}

			return node;
		}


		private NodeOld CreateNode(NodeName nodeName, ModelOld model, ModelViewDataOld modelViewData)
		{
			NodeName parentName = nodeName.ParentName;

			if (!model.Nodes.TryGetValue(parentName, out NodeOld parentNode))
			{
				parentNode = CreateNode(parentName, model, modelViewData);
			}

			NodeOld node = new NodeOld(nodeService, linkService, parentNode, nodeName, null);

			TrySetViewData(null, modelViewData, node, nodeName);

			parentNode.AddChild(node);
			model.AddNode(node);

			return node;
		}


		private static void TrySetViewData(
			Data.ViewData viewData,
			ModelViewDataOld modelViewData,
			NodeOld node,
			NodeName fullName)
		{
			if (viewData != null)
			{
				SetViewData(node, viewData);
			}
			else
			{
				if (modelViewData != null
						&& modelViewData.viewData.TryGetValue(fullName, out viewData))
				{
					SetViewData(node, viewData);
				}
			}
		}


		private static void SetViewData(NodeOld node, Data.ViewData viewData)
		{
			node.PersistentNodeBounds = ToBounds(viewData);
			node.PersistentScale = viewData.Scale;
			node.PersistentNodeColor = viewData.Color;
			node.PersistentOffset = new Point(viewData.OffsetX, viewData.OffsetY);
		}
	}
}