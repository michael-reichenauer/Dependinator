using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal class NodeService : INodeService
    {
        private readonly INodeLayoutService layoutService;
        private readonly Lazy<IModelLineService> modelLineService;
        private readonly Lazy<IModelLinkService> modelLinkService;
        private readonly IModelDatabase modelService;
        private readonly Lazy<INodeViewModelService> nodeViewModelService;


        public NodeService(
            IModelDatabase modelService,
            Lazy<IModelLineService> modelLineService,
            Lazy<IModelLinkService> modelLinkService,
            INodeLayoutService layoutService,
            Lazy<INodeViewModelService> nodeViewModelService)
        {
            this.modelService = modelService;
            this.modelLineService = modelLineService;
            this.modelLinkService = modelLinkService;
            this.layoutService = layoutService;
            this.nodeViewModelService = nodeViewModelService;
        }


        public Node Root => modelService.Root;

        public bool TryGetNode(NodeName nodeName, out Node node) => modelService.TryGetNode(nodeName, out node);
        public bool TryGetSavedNode(NodeName nodeName, out DataNode node) => modelService.TryGetSavedNode(nodeName, out node);
        public void SetIsChanged(Node node) => modelService.SetIsChanged(node);


        public void QueueNode(DataNode dataNode) => modelService.QueueNode(dataNode);


        public void RemoveAll()
        {
            Root?.ItemsCanvas?.RemoveAll();

            modelService.RemoveAll();
        }


        public IEnumerable<Node> AllNodes => modelService.AllNodes;


        public void AddNode(Node node, Node parentNode)
        {
            if (node.Bounds == RectEx.Zero)
            {
                if (modelService.TryGetSavedNode(node.Name, out DataNode savedNode))
                {
                    node.Bounds = savedNode.Bounds;
                    node.ScaleFactor = savedNode.Scale;
                    node.IsNodeHidden = savedNode.ShowState == Node.Hidden;
                }
            }
            
            modelService.Add(node);
            parentNode.AddChild(node);

            CreateNodeViewModel(node);

            AddNodeToParentCanvas(node, parentNode);

            if (modelService.TryGetQueuedLinesAndLinks(
                node.Name,
                out IReadOnlyList<DataLine> lines,
                out IReadOnlyList<DataLink> links))
            {
                lines.ForEach(line => modelLineService.Value.AddOrUpdateLine(line, node.Stamp));
                links.ForEach(link => modelLinkService.Value.AddOrUpdateLink(link, node.Stamp));
                modelService.RemovedQueuedNode(node.Name);
            }
        }


        public void RemoveNode(Node node)
        {
            modelService.Remove(node);
            node.Parent?.RemoveChild(node);

            if (node.ItemsCanvas != null)
            {
                node.Parent?.ItemsCanvas.RemoveChildCanvas(node.ItemsCanvas);
            }

            RemoveNodeFromParentCanvas(node);
        }


        public void UpdateNodeTypeIfNeeded(Node node, NodeType nodeType)
        {
            if (node.NodeType != nodeType)
            {
                Log.Warn($"Node type has changed for {node} to {node.NodeType}->{nodeType}");

                node.NodeType = nodeType;

                RemoveNodeFromParentCanvas(node);
                CreateNodeViewModel(node);
                AddNodeToParentCanvas(node, node.Parent);
            }
        }


        public void SetLayoutDone() => AllNodes.ForEach(node =>node.IsLayoutCompleted = true);


        public Node GetParentNode(NodeName parentName, NodeType childNodeType, bool isQueued)
        {
            if (modelService.TryGetNode(parentName, out Node parent))
            {
                if (isQueued && parentName == NodeName.Root)
                {
                    return GetParentNode(NodeName.From("$References"), NodeType.Group, false);
                }

                return parent;
            }

            NodeType parentNodeType = GetParentNodeType(parentName, childNodeType);

            NodeName grandParentName = parentName.ParentName;
            Node grandParent = GetParentNode(grandParentName, parentNodeType, isQueued);
            
            parent = new Node(parentName);
            parent.NodeType = parentNodeType;

            AddNode(parent, grandParent);
            return parent;
        }


        private static NodeType GetParentNodeType(NodeName parentName, NodeType childNodeType)
        {
            if (parentName?.FullName.EndsWith(".$private") ?? false)
            {
                return NodeType.NameSpace;
            }

            return childNodeType.IsMember() ? NodeType.Type : NodeType.NameSpace;
        }


        private void AddNodeToParentCanvas(Node node, Node parentNode)
        {
            try
            {
                layoutService.SetLayout(node.ViewModel);

                ItemsCanvas parentCanvas = parentNode.ItemsCanvas;

                parentCanvas.AddItem(node.ViewModel);
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed adding {node} to parent {parentNode}");
                throw;
            }
        }


        private void CreateNodeViewModel(Node node)
        {
            if (node.NodeType.IsMember())
            {
                node.ViewModel = new MemberNodeViewModel(nodeViewModelService.Value, node);
            }
            else if (node.NodeType.IsType())
            {
                node.ViewModel = new TypeViewModel(nodeViewModelService.Value, node);
                node.ItemsCanvas = GetItemsCanvas(node);
            }
            else
            {
                node.ViewModel = new NamespaceViewModel(nodeViewModelService.Value, node);
                node.ItemsCanvas = GetItemsCanvas(node);
            }
        }


        private ItemsCanvas GetItemsCanvas(Node node)
        {
            // First try get existing items canvas
            if (node.ItemsCanvas != null)
            {
                return node.ItemsCanvas;
            }

            // The node does not yet have a canvas. So we need to get the parent canvas and
            // then create a child canvas for this node.
            ItemsCanvas parentCanvas = GetItemsCanvas(node.Parent);

            // Creating the child canvas to be the children canvas of the node
            node.ItemsCanvas = new ItemsCanvas(node.ViewModel, parentCanvas);
            node.ViewModel.ItemsViewModel = new ItemsViewModel(
                node.ItemsCanvas, node.ViewModel);

            if (Math.Abs(node.ScaleFactor) > 0.0000001)
            {
                node.ItemsCanvas.ScaleFactor = node.ScaleFactor;
            }

            //if (node.Offset != PointEx.Zero)
            //{
            //	node.ItemsCanvas.SetMoveOffset(node.Offset);
            //}

            return node.ItemsCanvas;
        }


        private static void RemoveNodeFromParentCanvas(Node node)
        {
            node.Parent?.ItemsCanvas?.RemoveItem(node.ViewModel);
        }
    }
}
