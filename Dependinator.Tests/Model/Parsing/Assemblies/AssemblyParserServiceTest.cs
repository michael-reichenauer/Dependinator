using System.Threading.Channels;
using Dependinator.Parsing;
using Dependinator.Parsing.Assemblies;

namespace DependinatorLib.Tests.Model.Parsing.Assemblies;

public class AssemblyFileParserServiceTest
{
    // [Fact]
    // public async Task ParserTest()
    // {
    //     var parser = new AssemblyParserService();
    //     var channel = Channel.CreateUnbounded<IItem>();

    //     Assert.True(Try(await parser.ParseAsync(Path.Combine(AppContext.BaseDirectory, "Dependinator.dll"), channel)));
    //     channel.Writer.Complete();

    //     var list = await channel.Reader.ReadAllAsync().ToList();

    //     Assert.True(list.Any());
    // }
}
