using Dependinator.Utils;


namespace Dependinator.ModelViewing
{
    internal class LineId : Equatable<LineId>
    {
        private readonly NodeName source;
        private readonly NodeName target;


        public LineId(NodeName source, NodeName target)
        {
            this.source = source;
            this.target = target;

            IsEqualWhenSame(source, target);
        }


        public override string ToString() => $"{source}->{target}";
    }
}
