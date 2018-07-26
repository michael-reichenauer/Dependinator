using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
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
