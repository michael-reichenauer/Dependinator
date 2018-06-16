using System.Collections.Generic;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelLineService
	{
		void UpdateLine(DataLine dataLine, int stamp);
		void AddLinkLines(Link link);
		void RemoveLine(Line line);
		void AddLineViewModel(Line line);
		IEnumerable<LinkSegment> GetLinkSegments(Link link);
		void UpdateLines(Node node);
	}
}