using System;
using System.Collections.Generic;
using System.Windows.Media;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal interface IReferenceItemService
	{
		//IEnumerable<ReferenceItem> GetSourceLinkItems(Line line, Node baseNode);
		//IEnumerable<ReferenceItem> GetTargetLinkItems(Line line, Node baseNode);
		IEnumerable<ReferenceItem> GetReferences(Node node, ReferenceOptions option);
		//IEnumerable<ReferenceItem> GetOutgoingReferences(Node node, ReferenceOptions option);

		Brush ItemTextBrush();
		Brush ItemTextHiddenBrush();
		Brush ItemTextLowBrush();
	}
}