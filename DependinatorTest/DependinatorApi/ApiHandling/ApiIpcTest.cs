using System;
using DependinatorApi.ApiHandling;
using NUnit.Framework;


namespace DependinatorTest.DependinatorApi.ApiHandling
{
	[TestFixture]
	public class ApiIpcTest
	{
		[Test]
		public void Test2()
		{
			TestApiService instanceService = new TestApiService();

			string serverId = Guid.NewGuid().ToString();

			using (ApiIpcServer apiIpcServer = new ApiIpcServer(serverId))
			using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverId))
			{
				if (apiIpcServer.TryPublishService<ITestApiService>(instanceService))
				{
					if (ApiIpcClient.IsServerRegistered(serverId))
					{
						// Another instance for that working folder is already running, activate that.
						ITestApiService service = apiIpcClient.Service<ITestApiService>();

						string doubleName = service.GetDoubleName("hej");
						Assert.AreEqual("hejhej", doubleName);
						return;
					}
				}
			}

			Assert.Fail("Error");
		}
	}


	public interface ITestApiService
	{
		string GetDoubleName(string name);
	}


	internal class TestApiService : ApiIpcService, ITestApiService
	{
		public string GetDoubleName(string name)
		{
			return name + name;
		}
	}
}