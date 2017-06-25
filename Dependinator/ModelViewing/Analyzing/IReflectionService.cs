using Dependinator.ModelViewing.Serializing;


namespace Dependinator.ModelViewing.Analyzing
{
	internal interface IReflectionService
	{
		DataModel Analyze(string path);
	}
}