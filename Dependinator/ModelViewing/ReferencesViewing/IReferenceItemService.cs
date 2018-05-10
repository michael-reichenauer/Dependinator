using System;
using System.Collections.Generic;
using System.Windows.Media;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal interface IReferenceItemService
	{
		IEnumerable<ReferenceItem> GetSourceLinkItems(Line line);
		IEnumerable<ReferenceItem> GetTargetLinkItems(Line line);
		IEnumerable<ReferenceItem> GetIncomingReferences(Node node);
		IEnumerable<ReferenceItem> GetOutgoingReferences(Node node);



		Brush ItemTextBrush();
		Brush ItemTextHiddenBrush();
	}
}