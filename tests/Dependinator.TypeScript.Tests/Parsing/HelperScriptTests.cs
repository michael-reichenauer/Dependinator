using Dependinator.TypeScript.Parsing;

namespace Dependinator.TypeScript.Tests.Parsing;

public class HelperScriptTests
{
    [Fact]
    public void GetPath_ShouldGiveTheSameExistingScript_EachTime()
    {
        var path = AssertOk(HelperScript.GetPath());

        Assert.True(File.Exists(path));
        Assert.Matches(@"ts-parser-[0-9a-f]{16}\.js$", path);
        Assert.Equal(path, AssertOk(HelperScript.GetPath()));
    }

    [Fact]
    public void DeleteUnusedVersions_ShouldKeepTheScriptsOtherVersionsStillUse()
    {
        // Several versions of the app run side by side (VS Code's release and pre-release)
        var folder = Path.Combine(Path.GetTempPath(), $"dep-helpers-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(folder);
            var now = DateTime.UtcNow;
            var own = Script("ts-parser-1111.js", now.AddDays(-90));
            var inUse = Script("ts-parser-2222.js", now.AddDays(-2));
            var unused = Script("ts-parser-3333.js", now.AddDays(-40));
            var leftover = Script("ts-parser-4444.js.123.tmp", now.AddDays(-40));
            var unrelated = Script("notes.txt", now.AddDays(-400));

            HelperScript.DeleteUnusedVersions(folder, own, now.AddDays(-30));

            Assert.True(File.Exists(own));
            Assert.True(File.Exists(inUse));
            Assert.False(File.Exists(unused));
            Assert.False(File.Exists(leftover));
            Assert.True(File.Exists(unrelated));

            string Script(string name, DateTime usedUtc)
            {
                var path = Path.Combine(folder, name);
                File.WriteAllText(path, "");
                File.SetLastWriteTimeUtc(path, usedUtc);
                return path;
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
