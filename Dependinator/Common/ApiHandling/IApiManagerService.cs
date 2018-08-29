namespace Dependinator.Common.ApiHandling
{
    internal interface IApiManagerService
    {
        void Register();

        string GetCurrentInstanceServerName();
        string GetCurrentInstanceServerName(string modelFilePath);
    }
}
