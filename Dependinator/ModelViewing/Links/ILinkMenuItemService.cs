using System.Collections.Generic;


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