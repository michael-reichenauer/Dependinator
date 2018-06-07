using Dependinator.Utils.OsSystem;
using NUnit.Framework;


namespace DependinatorTest.Utils
{
	[TestFixture, Explicit]
	public class ProcessTest
	{
		[Test]
		public void Test()
		{
			Cmd cmd = new Cmd();

			CmdResult result = cmd.Run("git", "version");
			Assert.AreEqual(0, result.ExitCode);

			Assert.That(result.Output, Does.StartWith("git version 2."));
		}
	}
}
