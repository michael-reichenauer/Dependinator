using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.Nodes.Private
{
	internal static class NodeComparer
	{
		public static IComparer<Node> Comparer(Node parent)
		{
			return Compare.With<Node>((n1, n2) => CompareNodes(parent, n1, n2));
		}


		private static int CompareNodes(Node parent, Node e1, Node e2)
		{
			if (e1 == e2)
			{
				return 0;
			}

			if (parent.Name.DisplayShortName == "Dependinator")
			{
				
			}

			Line e1ToE2 = e1.SourceLines.FirstOrDefault(r => r.Target == e2);
			Line e2ToE1 = e2.SourceLines.FirstOrDefault(r => r.Target == e1);

			int e1ToE2Count = e1ToE2?.LinkCount ?? 0;
			int e2ToE1Count = e2ToE1?.LinkCount ?? 0;

			if (e1ToE2Count > e2ToE1Count)
			{
				return -1;
			}
			else if (e1ToE2Count < e2ToE1Count)
			{
				return 1;
			}

			Line parentToE1 = parent.SourceLines.FirstOrDefault(r => r.Target == e1);
			Line parentToE2 = parent.SourceLines.FirstOrDefault(r => r.Target == e2);

			int parentToE1Count = parentToE1?.LinkCount ?? 0;
			int parentToE2Count = parentToE2?.LinkCount ?? 0;

			if (parentToE1Count > parentToE2Count)
			{
				return -1;
			}
			else if (parentToE1Count < parentToE2Count)
			{
				return 1;
			}

			Line e1ToParent = e1.SourceLines.FirstOrDefault(r => r.Target == parent);
			Line e2ToParent = e2.SourceLines.FirstOrDefault(r => r.Target == parent);

			int e1ToParentCount = e1ToParent?.LinkCount ?? 0;
			int e2ToParentCount = e2ToParent?.LinkCount ?? 0;

			if (e1ToParentCount > e2ToParentCount)
			{
				return -1;
			}
			else if (e1ToParentCount < e2ToParentCount)
			{
				return 1;
			}

			return 0;
		}
	}
}