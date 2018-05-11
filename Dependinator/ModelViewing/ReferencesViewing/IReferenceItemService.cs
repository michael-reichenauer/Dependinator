using System;
using System.Collections.Generic;
using System.Windows.Media;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal interface IReferenceItemService
	{
		IEnumerable<ReferenceItem> GetReferences(Node node, ReferenceOptions options);
		IEnumerable<ReferenceItem> GetReferences(Line line, ReferenceOptions options);

		Brush ItemTextBrush();
		Brush ItemTextHiddenBrush();
		Brush ItemTextLowBrush();
	}
}