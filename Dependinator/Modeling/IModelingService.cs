namespace Dependinator.Modeling
{
	internal interface IModelingService
	{
		ModelOld Analyze(string path, ModelViewDataOld modelViewData);

		ModelViewDataOld ToViewData(ModelOld model);

		void Serialize(ModelOld model, string path);

		bool TryDeserialize(string path, out ModelOld model);
	}
}