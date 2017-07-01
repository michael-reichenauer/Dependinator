using Dependinator.Modeling.Serializing;

namespace Dependinator.Modeling.Analyzing
{
	internal interface IReflectionService
	{
		Data.Model Analyze(string path);
	}
}