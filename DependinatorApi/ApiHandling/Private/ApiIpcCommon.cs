using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace DependinatorApi.ApiHandling.Private
{
    internal static class ApiIpcCommon
    {
        private static readonly string ApiGuid = "AAFB58B3-34AF-408B-92BD-55DC977E5250";


        internal static bool IsServerRegistered(string serverId)
        {
            serverId = GetServerId(serverId);

            using (new Mutex(true, serverId, out var isMutexCreated))
            {
                if (isMutexCreated)
                {
                    return false;
                }
            }

            return true;
        }


        internal static string GetServiceName<TRemoteService>(string serverId) =>
            serverId + typeof(TRemoteService).FullName;


        internal static string GetServerId(string text) => AsSha2Text(ApiGuid + Environment.UserName + text);


        private static string AsSha2Text(string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text.ToLower());

            SHA256Managed shaService = new SHA256Managed();
            byte[] shaHash = shaService.ComputeHash(textBytes, 0, textBytes.Length);

            StringBuilder hashText = new StringBuilder();
            foreach (byte b in shaHash)
            {
                hashText.Append(b.ToString("x2"));
            }

            return hashText.ToString();
        }
    }
}
