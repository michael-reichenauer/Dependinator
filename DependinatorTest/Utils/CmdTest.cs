using Dependiator.Utils;
using NUnit.Framework;


namespace DependiatorTest.Utils
{
	[TestFixture]
	public class ProcessTest
	{
		[Test]
		public void Test()
		{
			Cmd cmd = new Cmd();

			CmdResult result = cmd.Run("git", "version");
			Assert.AreEqual(0, result.ExitCode);

			Assert.That(result.Output, Is.StringStarting("git version 2."));
		}
	}
}
