using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal interface IModelService
	{
		Model ToModel(DataModel dataModel, ModelViewData modelViewData);

		DataModel ToDataModel(Model elementTree);

		ModelViewData ToViewData(Model elementTree);
	}
}