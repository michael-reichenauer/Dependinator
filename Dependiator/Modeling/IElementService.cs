using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal interface IElementService
	{
		ElementTree ToElementTree(Data data);

		Data ToData(ElementTree elementTree);
	}
}