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

            if (IgnoredTypes.IsIgnored(ns))
                continue;

            var (comments, fileSpan) = CommentExtractor.GetNamespaceComments(ns);
            if (fileSpan is null)
                continue;

            var (description, lineDescriptions) = GetDescriptions(comments);
            var fullName = Names.GetFullNamespaceName(ns, moduleName);

            yield return new Item(
                new Node(
                    fullName,
                    new NodeProperties
                    {
                        Type = NodeType.Namespace,
                        Description = description,
                        FileSpan = fileSpan,
                    }
                ),
                null
            );

            foreach (var (target, text) in lineDescriptions)
                yield return new Item(null, null, new LineDescription(fullName, target, text));
        }
    }

    // The namespace's description is the comment above its declaration. Comments above it in more
    // than one file are all shown, each after its location: one of them is likely a note about its
    // file rather than a description of the namespace, and the user can see that and where to fix it.
    static (string? Description, IReadOnlyList<(string Target, string Text)> LineDescriptions) GetDescriptions(
        IReadOnlyList<(string Comment, FileSpan Span)> comments
    )
    {
        var parsed = comments.Select(c => (Parsed: CommentDescriptions.Parse(c.Comment), c.Span)).ToList();
        var descriptions = parsed
            .Where(p => p.Parsed.NodeDescription is not null)
            .Select(p => (Text: p.Parsed.NodeDescription!, p.Span))
            .ToList();

        var description = descriptions.Count switch
        {
            0 => null,
            1 => descriptions[0].Text,
            _ => string.Join(Environment.NewLine, descriptions.Select(d => $"{Location(d.Span)}: {d.Text}")),
        };
        return (description, [.. parsed.SelectMany(p => p.Parsed.LineDescriptions)]);
    }

    // "Util.cs:3": the file and line of a namespace declaration
    static string Location(FileSpan span) => $"{Path.GetFileName(span.Path)}:{span.StartLine + 1}";

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
