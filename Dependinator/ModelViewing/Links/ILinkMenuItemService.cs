using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links
{
	internal interface ILinkMenuItemService
	{
		IEnumerable<LinkItem> GetTargetLinkItems(IEnumerable<Line> lines);

		IEnumerable<LinkItem> GetSourceLinkItems(IEnumerable<Line> lines);
	}
}