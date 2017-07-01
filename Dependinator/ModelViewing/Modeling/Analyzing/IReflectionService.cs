using Dependinator.ModelViewing.Modeling.Serializing;

namespace Dependinator.ModelViewing.Modeling.Analyzing
{
	internal interface IReflectionService
	{
		Data.Model Analyze(string path);
	}
}