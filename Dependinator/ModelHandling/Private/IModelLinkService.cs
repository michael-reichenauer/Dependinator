using System.Collections.Generic;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelViewing.Links;


namespace Dependinator.ModelHandling.Private
{
	internal interface IModelLinkService
	{
		void UpdateLink(ModelLink modelLink, int stamp);
		void UpdateLine(ModelLine modelLine, int stamp);
		void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks);
		void ResetLayout(List<Link> links);
		void AddLineViewModel(Line line);
	}
}