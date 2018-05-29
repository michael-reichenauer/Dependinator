using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal interface ILineMenuItemService
	{
		IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(Line line);

		IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(Line line);

		IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(IEnumerable<Line> lines);

		IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(IEnumerable<Line> lines);
	}
}