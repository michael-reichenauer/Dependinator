//using System.Collections.Generic;
//using System.Linq;
//using Dependinator.ModelViewing.Links.Private;
//using Dependinator.ModelViewing.Nodes;
//using Dependinator.Utils;


//namespace Dependinator.ModelViewing.Links
//{
//	internal class LinkOld : Equatable<LinkOld>
//	{
//		private readonly List<LinkLineOld> lines = new List<LinkLineOld>();
//		private IReadOnlyList<LinkSegmentOld> currentLinkSegments;

//		public LinkOld(NodeOld source, NodeOld target)
//		{
//			Source = source;
//			Target = target;
//			IsEqualWhenSame(Source, Target);
//		}


//		public NodeOld Source { get; }

//		public NodeOld Target { get; }

//		public IReadOnlyList<LinkLineOld> Lines => lines;
//		public IReadOnlyList<LinkSegmentOld> LinkSegments => currentLinkSegments;


//		public bool TryAddLinkLine(LinkLineOld line) => lines.TryAdd(line);

//		public bool Remove(LinkLineOld line) => lines.Remove(line);


//		public override string ToString() => $"{Source} -> {Target}";


//		public void SetLinkSegments(IReadOnlyList<LinkSegmentOld> linkSegments)
//		{
//			currentLinkSegments = linkSegments;
//		}
//	}
//}