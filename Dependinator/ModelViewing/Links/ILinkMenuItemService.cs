using System.Collections.Generic;
using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelViewing.Links
{
	internal interface ILinkMenuItemService
	{
		IEnumerable<LinkItem> GetSourceLinkItems(Line line);

		IEnumerable<LinkItem> GetTargetLinkItems(Line line);

		IEnumerable<LinkItem> GetSourceLinkItems(IEnumerable<Line> lines);

		IEnumerable<LinkItem> GetTargetLinkItems(IEnumerable<Line> lines);
	}
}