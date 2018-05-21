using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal interface IReferenceItemService
	{
		Task<IReadOnlyList<ReferenceItem>> GetReferencesAsync(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter);

		Brush ItemTextBrush();
		Brush ItemTextHiddenBrush();

		void ShowCode(Node node);
	}
}