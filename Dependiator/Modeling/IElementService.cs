using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal interface IElementService
	{
		ElementTree ToElementTree(DataModel data, DataModel oldData);

		DataModel ToData(ElementTree elementTree);
	}
}