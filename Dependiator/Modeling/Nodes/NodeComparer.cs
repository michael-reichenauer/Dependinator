using System.Collections.Generic;
using Dependiator.Utils;


namespace Dependiator.Modeling.Nodes
{
	internal class NodeComparer
	{
		public static IComparer<Node> Comparer(Node parent)
		{
			return Compare.With<Node>((n1, n2) => CompareNodes(parent, n1, n2));
		}


		private static int CompareNodes(Node parent, Node e1, Node e2)
		{
			//Link e1ToE2 = parent.Links
			//	.FirstOrDefault(r => r.Source == e1 && r.Target == e2);
			//Link e2ToE1 = parent.Links
			//	.FirstOrDefault(r => r.Source == e2 && r.Target == e1);

			//int e1ToE2Count = e1ToE2?.NodeLinks.Count ?? 0;
			//int e2ToE1Count = e2ToE1?.NodeLinks.Count ?? 0;

			//if (e1ToE2Count > e2ToE1Count)
			//{
			//	return -1;
			//}
			//else if (e1ToE2Count < e2ToE1Count)
			//{
			//	return 1;
			//}

			//Link parentToE1 = parent.Links
			//	.FirstOrDefault(r => r.Source == parent && r.Target == e1);
			//Link parentToE2 = parent.Links
			//	.FirstOrDefault(r => r.Source == parent && r.Target == e2);

			//int parentToE1Count = parentToE1?.NodeLinks.Count ?? 0;
			//int parentToE2Count = parentToE2?.NodeLinks.Count ?? 0;

			//if (parentToE1Count > parentToE2Count)
			//{
			//	return -1;
			//}
			//else if (parentToE1Count < parentToE2Count)
			//{
			//	return 1;
			//}

			//Link e1ToParent = parent.Links
			//	.FirstOrDefault(r => r.Source == e1 && r.Target == parent);
			//Link e2ToParent = parent.Links
			//	.FirstOrDefault(r => r.Source == e2 && r.Target == parent);

			//int e1ToParentCount = e1ToParent?.NodeLinks.Count ?? 0;
			//int e2ToParentCount = e2ToParent?.NodeLinks.Count ?? 0;

			//if (e1ToParentCount > e2ToParentCount)
			//{
			//	return -1;
			//}
			//else if (e1ToParentCount < e2ToParentCount)
			//{
			//	return 1;
			//}

			return 0;
		}
	}
}