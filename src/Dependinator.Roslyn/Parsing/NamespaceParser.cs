using Dependinator.Core.Parsing.Utils;
using Microsoft.CodeAnalysis;

namespace Dependinator.Roslyn.Parsing;

static class NamespaceParser
{
    public static IEnumerable<Item> ParseNamespaces(Compilation compilation, string moduleName)
    {
        foreach (var ns in GetAllNamespaces(compilation.Assembly.GlobalNamespace))
        {
            if (ns.IsGlobalNamespace)
                continue;

            var comment = CommentExtractor.GetNamespaceCommentOrNull(ns);
            if (comment is null)
                continue; // Only emit items when the user actually documented the namespace

            var (description, lineDescriptions) = CommentDescriptions.Parse(comment);
            var fullName = Names.GetFullNamespaceName(ns, moduleName);

            if (description is not null)
                yield return new Item(
                    new Node(fullName, new NodeProperties { Type = NodeType.Namespace, Description = description }),
                    null
                );

            foreach (var (target, text) in lineDescriptions)
                yield return new Item(null, null, new LineDescription(fullName, target, text));
        }
    }

    static IEnumerable<INamespaceSymbol> GetAllNamespaces(INamespaceSymbol ns)
    {
        yield return ns;

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var descendant in GetAllNamespaces(child))
                yield return descendant;
        }
    }
}
