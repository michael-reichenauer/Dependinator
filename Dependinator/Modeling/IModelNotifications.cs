using System.Collections.Generic;

namespace Dependinator.Modeling
{
	internal interface IModelNotifications
	{
		void UpdateNodes(IReadOnlyList<Node2> nodes);

		void UpdateLinks(IReadOnlyList<Link2> nodes);
	}
}
