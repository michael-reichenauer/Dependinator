using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Searching.Private
{
	class SearchService : ISearchService
	{
		private readonly IModelService modelService;


		public SearchService(IModelService modelService)
		{
			this.modelService = modelService;
		}

		public IEnumerable<NodeName> Search(string text)
		{
			text = text.Trim();
			if (string.IsNullOrEmpty(text))
			{
				return Enumerable.Empty<NodeName>();
			}

			return modelService.AllNodes
				.Where(node => NameContainsText(text, node))
				.Select(node =>node.Name);
		}


		private static bool NameContainsText(string text, Node node)
		{
			string name = node.Name.DisplayLongName;
			int index = name.IndexOf("(");
			if (index > -1)
			{
				// Do not search in parameters
				return -1 != name.IndexOf(text, 0, index, StringComparison.OrdinalIgnoreCase);
			}

			return -1 != name.IndexOf(text, StringComparison.OrdinalIgnoreCase);
		}
	}
}