using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    internal class DataLink : Equatable<DataLink>, IDataItem
    {
        public DataLink(
            DataNodeName source,
            DataNodeName target,
            NodeType targetType,
            bool isAdded = false)
        {
            Source = source;
            Target = target;
            TargetType = targetType;
            IsAdded = isAdded;

            IsEqualWhenSame(Source, Target);
        }


        public DataNodeName Source { get; }
        public DataNodeName Target { get; }
        public NodeType TargetType { get; }

        public bool IsAdded { get; }

        public override string ToString() => $"{Source}->{Target}";
    }
}
