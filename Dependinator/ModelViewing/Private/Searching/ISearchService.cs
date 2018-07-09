using System.Collections.Generic;


namespace Dependinator.ModelViewing.Private.Searching
{
	internal interface ISearchService
	{
		IEnumerable<NodeName> Search(string text);
	}
}