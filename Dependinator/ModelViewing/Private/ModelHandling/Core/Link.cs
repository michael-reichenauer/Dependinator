using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Core
{
    internal class Link : Equatable<Link>, IEdge
    {
        public Link(Node source, Node target)
        {
            Source = source;
            Target = target;

            IsEqualWhenSame(source, target);
        }


        public int Stamp { get; set; }

        public bool IsHidden => Source.IsHidden || Target.IsHidden;
        public Node Target { get; }
        public Node Source { get; }

        public override string ToString() => $"{Source}->{Target}";
    }
}
