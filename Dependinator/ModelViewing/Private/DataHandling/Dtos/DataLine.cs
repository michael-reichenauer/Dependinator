using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    internal class DataLine : Equatable<DataLine>, IDataItem
    {
        public DataLine(
            DataNodeName source,
            DataNodeName target,
            IReadOnlyList<Point> points,
            int linkCount)
        {
            Source = source;
            Target = target;
            Points = points;
            LinkCount = linkCount;

            IsEqualWhenSame(Source, Target);
        }


        public DataNodeName Source { get; }
        public DataNodeName Target { get; }
        public IReadOnlyList<Point> Points { get; }
        public int LinkCount { get; }

        public override string ToString() => $"{Source}->{Target}";
    }
}
