using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelLinkService
	{
		void UpdateLink(ModelLink modelLink, int stamp);
		void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks);
		void ResetLayout(List<Link> links);
	}
}