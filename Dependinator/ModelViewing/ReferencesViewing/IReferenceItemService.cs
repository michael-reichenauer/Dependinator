using System.Collections.Generic;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal interface IReferenceItemService
	{
		IEnumerable<ReferenceItem> GetReferences(IEnumerable<Line> lines, ReferenceOptions options);
		Brush ItemTextBrush();
		Brush ItemTextHiddenBrush();
		Brush ItemTextLowBrush();
		void ShowCode(Node node);
	}
}