using System.Collections;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal interface IElementService
	{
		ElementTree ToElementTree(Data.Model data, ModelViewData modelViewData);

		Data.Model ToData(ElementTree elementTree);

		ModelViewData ToViewData(ElementTree elementTree);
	}
}