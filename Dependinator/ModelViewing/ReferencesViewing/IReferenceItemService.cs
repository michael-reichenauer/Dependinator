using System;
using System.Collections.Generic;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal interface IReferenceItemService
	{
		IEnumerable<ReferenceItemViewModel> GetSourceLinkItems(Line line);
		IEnumerable<ReferenceItemViewModel> GetTargetLinkItems(Line line);
		IEnumerable<ReferenceItemViewModel> GetSourceLinkItems(IEnumerable<Line> lines);
		IEnumerable<ReferenceItemViewModel> GetTargetLinkItems(IEnumerable<Line> lines);


		IEnumerable<ReferenceItemViewModel> GetLinkItems(
			IEnumerable<Line> lines,
			Func<IEdge, Node> endPoint);
	}
}