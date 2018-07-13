namespace Dependinator.Api
{
	internal interface IApiManagerService
	{
		void Register();

		string GetCurrentInstanceServerName();
	}
}