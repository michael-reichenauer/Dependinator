using System.Collections.Generic;

namespace Dependinator.Modeling
{
	internal interface IModelNotifications
	{
		void UpdateNodes(IReadOnlyList<Node> nodes);

		void UpdateLinks(IReadOnlyList<Link> nodes);
	}
}
