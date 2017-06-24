using Dependinator.Modeling.Serializing;


namespace Dependinator.Modeling
{
	internal interface IModelService
	{
		Model ToModel(DataModel dataModel, ModelViewData modelViewData);

		DataModel ToDataModel(Model elementTree);

		ModelViewData ToViewData(Model elementTree);
	}
}