using Microsoft.CodeAnalysis;

namespace DependinatorCore.Parsing.Sources;

class MemberParser
{
    public static IEnumerable<Parsing.Item> ParseMember(ISymbol member, string fullTypeName)
    {
        // only members declared on this type (not inherited)

        // var memberSpans = Locations.GetLocationSpans(member);
        // if (!memberSpans.Any())
        //     yield break;
        // var firstMemberSpan = memberSpans.First();

        yield return new Parsing.Item(
            new Node(
                Names.GetFullMemberName(member, fullTypeName),
                new NodeAttributes
                {
                    // FileSpan = new FileSpan(
                    //     firstMemberSpan.Path,
                    //     firstMemberSpan.StartLinePosition.Line,
                    //     firstMemberSpan.EndLinePosition.Line
                    // ),
                }
            ),
            null
        );
    }
}
