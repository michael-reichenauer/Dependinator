namespace Dependinator.Api.ApiHandling
{
	internal interface IApiManagerService
	{
		void Register();

		string GetCurrentInstanceServerName();
		string GetCurrentInstanceServerName(string modelFilePath);
	}
}