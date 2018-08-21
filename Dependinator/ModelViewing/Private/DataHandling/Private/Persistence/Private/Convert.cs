using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes;
using Dependinator.Utils;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class Convert
    {
        public static IDataItem ToModelItem(JsonCacheTypes.Item item)
        {
            if (item.Node != null)
            {
                return ToModelNode(item.Node);
            }

            if (item.Link != null)
            {
                return ToModelLink(item.Link);
            }

            if (item.Line != null)
            {
                return ToModelLine(item.Line);
            }

            return null;
        }


        public static JsonCacheTypes.Item ToCacheJsonItem(IDataItem item)
        {
            switch (item)
            {
                case DataNode modelNode:
                    return new JsonCacheTypes.Item { Node = ToCacheJsonNode(modelNode) };
                case DataLink modelLink:
                    return new JsonCacheTypes.Item { Link = ToJsonLink(modelLink) };
                case DataLine modelLine:
                    return new JsonCacheTypes.Item { Line = ToCacheJsonLine(modelLine) };
                default:
                    throw Asserter.FailFast($"Unsupported item {item?.GetType()}");
            }
        }


        public static DataNode ToDataNode(JsonSaveTypes.Node node) => new DataNode(
            (DataNodeName)node.N,
            null,
            NodeType.None)
        {
            Bounds = node.B != null ? Rect.Parse(node.B) : RectEx.Zero,
            Scale = node.S,
            Color = node.C,
            ShowState = node.St
        };


        private static DataNode ToModelNode(JsonCacheTypes.Node node) => new DataNode(
            (DataNodeName)node.Name,
            node.Parent != null ? (DataNodeName)node.Parent : null,
            FromNodeTypeText(node.Type))
        {
            Description = node.Description,
            Bounds = node.Bounds != null ? Rect.Parse(node.Bounds) : RectEx.Zero,
            Scale = node.Scale,
            Color = node.Color,
            ShowState = node.State
        };


        private static JsonCacheTypes.Node ToCacheJsonNode(DataNode node) => new JsonCacheTypes.Node
        {
            Name = (string)node.Name,
            Parent = (string)node.Parent,
            Type = ToNodeTypeText(node.NodeType),
            Description = node.Description,
            Bounds = node.Bounds != RectEx.Zero ? node.Bounds.AsIntString() : null,
            Scale = node.Scale,
            Color = node.Color,
            State = node.ShowState
        };


        public static JsonSaveTypes.Node ToSaveJsonNode(DataNode node) => new JsonSaveTypes.Node
        {
            N = node.Name.AsId(),
            B = node.Bounds != RectEx.Zero ? node.Bounds.AsIntString() : null,
            S = node.Scale.Round(3),
            St = node.ShowState
        };


        public static JsonSaveTypes.Line ToSaveJsonLine(DataLine line) => new JsonSaveTypes.Line
        {
            T = line.Target.AsId(),
            P = ToJsonPoints(line.Points)
        };


        private static NodeType FromNodeTypeText(string nodeType)
        {
            switch (nodeType)
            {
                case null:
                    return NodeType.None;
                case JsonCacheTypes.NodeType.Solution:
                    return NodeType.Solution;
                case JsonCacheTypes.NodeType.SolutionFolder:
                    return NodeType.SolutionFolder;
                case JsonCacheTypes.NodeType.Assembly:
                    return NodeType.Assembly;
                case JsonCacheTypes.NodeType.Group:
                    return NodeType.Group;
                case JsonCacheTypes.NodeType.Dll:
                    return NodeType.Dll;
                case JsonCacheTypes.NodeType.Exe:
                    return NodeType.Exe;
                case JsonCacheTypes.NodeType.NameSpace:
                    return NodeType.NameSpace;
                case JsonCacheTypes.NodeType.Type:
                    return NodeType.Type;
                case JsonCacheTypes.NodeType.Member:
                    return NodeType.Member;
                default:
                    throw Asserter.FailFast($"Unexpected type {nodeType}");
            }
        }


        private static string ToNodeTypeText(NodeType nodeNodeType)
        {
            switch (nodeNodeType)
            {
                case NodeType.NameSpace:
                    return JsonCacheTypes.NodeType.NameSpace;
                case NodeType.Type:
                    return JsonCacheTypes.NodeType.Type;
                case NodeType.Member:
                    return JsonCacheTypes.NodeType.Member;
                case NodeType.None:
                    return null;
                case NodeType.Solution:
                    return JsonCacheTypes.NodeType.Solution;
                case NodeType.Assembly:
                    return JsonCacheTypes.NodeType.Assembly;
                case NodeType.Group:
                    return JsonCacheTypes.NodeType.Group;
                case NodeType.Dll:
                    return JsonCacheTypes.NodeType.Dll;
                case NodeType.Exe:
                    return JsonCacheTypes.NodeType.Exe;
                case NodeType.SolutionFolder:
                    return JsonCacheTypes.NodeType.SolutionFolder;
                case NodeType.PrivateMember:
                    return JsonCacheTypes.NodeType.Member;
                default:
                    throw Asserter.FailFast($"Unexpected type {nodeNodeType}");
            }
        }


        private static DataLink ToModelLink(JsonCacheTypes.Link link) => new DataLink(
            (DataNodeName)link.Source,
            (DataNodeName)link.Target,
            NodeType.None,
            true);


        private static JsonCacheTypes.Link ToJsonLink(DataLink link) => new JsonCacheTypes.Link
        {
            Source = (string)link.Source,
            Target = (string)link.Target
        };


        private static IDataItem ToModelLine(JsonCacheTypes.Line line) => new DataLine(
            (DataNodeName)line.Source,
            (DataNodeName)line.Target,
            ToLinePoints(line.Points),
            line.LinkCount);


        private static JsonCacheTypes.Line ToCacheJsonLine(DataLine line) => new JsonCacheTypes.Line
        {
            Source = (string)line.Source,
            Target = (string)line.Target,
            Points = ToJsonPoints(line.Points),
            LinkCount = line.LinkCount
        };


        private static IReadOnlyList<Point> ToLinePoints(IEnumerable<string> linePoints)
        {
            if (linePoints == null)
            {
                return new List<Point>();
            }

            return linePoints.Select(Point.Parse).ToList();
        }


        private static List<string> ToJsonPoints(IEnumerable<Point> points)
        {
            if (!points.Any())
            {
                return null;
            }

            return points.Select(point => point.AsString()).ToList();
        }
    }
}
