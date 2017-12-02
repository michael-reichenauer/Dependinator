using System.Collections.Generic;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
{
	internal interface IParserFactoryService
	{
		IReadOnlyList<AssemblyParser> CreateParsers(
			string filePath, ModelItemsCallback modelItemsCallback);
	}
}