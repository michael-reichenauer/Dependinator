using System.Threading.Tasks;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection
{
	internal interface IReflectionService
	{
		Task ParseAsync(string filePath, ModelItemsCallback modelItemsCallback);
	}
}