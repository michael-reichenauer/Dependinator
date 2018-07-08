using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Searching
{
	internal interface ISearchService
	{
		IEnumerable<NodeName> Search(string text);
	}
}