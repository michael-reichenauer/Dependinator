using System.Collections.Generic;

namespace Dependinator.Modeling
{
	internal interface IModelNotifications
	{
		void UpdateNodes(IReadOnlyList<DataNode> nodes);

		void UpdateLinks(IReadOnlyList<DataLink> links);
	}
}
