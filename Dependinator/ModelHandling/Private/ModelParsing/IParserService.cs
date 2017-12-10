using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.Private.ModelParsing
{
	internal interface IParserService
	{
		Task ParseAsync(string assemblyPath, ModelItemsCallback modelItemsCallback);
	}
}