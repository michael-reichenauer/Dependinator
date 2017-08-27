using System.Collections.Generic;
using Dependinator.ModelParsing;

namespace Dependinator.ModelViewing.Links
{
	internal interface ILinkService
	{
		void UpdateLink(ModelLink modelLink, int stamp);
		void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks);
		void ResetLayout(List<Link> links);
	}
}