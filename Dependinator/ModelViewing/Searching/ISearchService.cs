using System.Collections.Generic;


namespace Dependinator.ModelViewing.Searching
{
	internal interface ISearchService
	{
		IEnumerable<SearchEntry> Search(string text);
	}
}