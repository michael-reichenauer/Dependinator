using Dependinator.Utils;


namespace Dependinator.ModelViewing
{
    internal class LineId : Equatable<LineId>
    {
        public NodeName Source { get; }
        public NodeName Target { get; }


        public LineId(NodeName source, NodeName target)
        {
            this.Source = source;
            this.Target = target;

            IsEqualWhenSame(source, target);
        }


        public override string ToString() => $"{Source}->{Target}";
    }
}
