using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal class Convert
    {
        public static IEnumerable<IDataItem> ToDataItems(IEnumerable<Node> nodes)
        {
            return nodes.SelectMany(ToDataItems);
        }


        private static IEnumerable<IDataItem> ToDataItems(Node node)
        {
            yield return ToDataNode(node);

            foreach (var line in node.SourceLines.Where(line => !line.IsHidden))
            {
                yield return ToDataLine(line);
            }

            foreach (var link in node.SourceLinks)
            {
                yield return ToDataLink(link);
            }
        }


        private static DataNode ToDataNode(Node node)
        {
            return new DataNode(
                node.Name,
                node.Parent.Name,
                node.NodeType)
            {
                Description = node.Description,
                Bounds = node.ViewModel?.ItemBounds ?? node.Bounds,
                Scale = node.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.ScaleFactor,
                Color = node.ViewModel?.Color ?? node.Color,
                ShowState = node.IsNodeHidden ? Node.Hidden : null
            };
        }


        private static DataLine ToDataLine(Line line)
        {
            return new DataLine(
                line.Source.Name,
                line.Target.Name,
                line.View.MiddlePoints().ToList(),
                line.LinkCount);
        }


        private static DataLink ToDataLink(Link link) => new DataLink(link.Source.Name, link.Target.Name);
    }
}
