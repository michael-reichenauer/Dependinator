using System.Collections.Generic;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelLineService
	{
		void AddOrUpdateLine(DataLine dataLine, int stamp);
		void AddLinkLines(Link link);
		void RemoveLine(Line line);
		void AddLineViewModel(Line line);
		IEnumerable<LinkSegment> GetLinkSegments(Link link);
		void UpdateLines(Node node);
	}
}