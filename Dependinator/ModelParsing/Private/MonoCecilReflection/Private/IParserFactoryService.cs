using System.Collections.Generic;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal interface IParserFactoryService
	{
		IReadOnlyList<AssemblyParser> CreateParsers(
			string filePath, ModelItemsCallback modelItemsCallback);
	}
}