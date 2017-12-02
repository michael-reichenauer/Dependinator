using System.Threading.Tasks;
using Dependinator.ModelHandling.Core;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection
{
	internal interface IReflectionService
	{
		Task ParseAsync(string filePath, ModelItemsCallback modelItemsCallback);
	}
}