namespace DependinatorApi
{
	public interface IVsExtensionApi
	{
		void Activate();

		void ShowFile(string filePath, int lineNumber);
	}
}