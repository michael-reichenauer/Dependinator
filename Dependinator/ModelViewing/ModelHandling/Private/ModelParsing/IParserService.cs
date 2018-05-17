using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing
{
	internal interface IParserService
	{
		Task ParseAsync(string filePath, ModelItemsCallback modelItemsCallback);
	}
}