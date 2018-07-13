namespace Dependinator.Api.ApiHandling
{
	internal interface IApiManagerService
	{
		void Register();

		string GetCurrentInstanceServerName();
	}
}