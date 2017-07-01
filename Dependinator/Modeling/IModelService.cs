namespace Dependinator.Modeling
{
	internal interface IModelService
	{
		Model Analyze(string path, ModelViewData modelViewData);

		ModelViewData ToViewData(Model model);

		void Serialize(Model model, string path);

		bool TryDeserialize(string path, out Model model);
	}
}