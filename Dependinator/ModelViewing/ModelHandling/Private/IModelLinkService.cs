using System.Collections.Generic;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelLinkService
	{
		void UpdateLink(DataLink dataLink, int stamp);
		void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks);
		void Hide(Link link);
		void Show(Link link);
	}
}