using System.Net;


namespace Dependiator.Git
{
	internal interface ICredentialHandler
	{
		NetworkCredential GetCredential(string url, string usernameFromUrl);

		void SetConfirm(bool isConfirmed);
	}
}