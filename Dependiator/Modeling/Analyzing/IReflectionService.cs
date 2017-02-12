using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing
{
	internal interface IReflectionService
	{
		Data.Model Analyze(string path);
	}
}