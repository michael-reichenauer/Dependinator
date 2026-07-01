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

            var description = CommentExtractor.GetNamespaceCommentOrNull(ns);
            if (description is null)
                continue; // Only emit a node when the user actually documented the namespace

            var fullName = Names.GetFullNamespaceName(ns, moduleName);
            yield return new Item(
                new Node(fullName, new NodeProperties { Type = NodeType.Namespace, Description = description }),
                null
            );
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
