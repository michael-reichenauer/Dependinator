using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
	internal interface IModelLinkService
	{
		void AddOrUpdateLink(DataLink dataLink, int stamp);
		void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks);
		void Hide(Link link);
		void Show(Link link);
	}
}