using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependinatorCore.Parsing.Sources.Roslyn;

class MethodParser
{
    public static IEnumerable<Item> ParseMethodLinks(
        IMethodSymbol member,
        string fullMethodName,
        Compilation compilation
    )
    {
        var sentLinks = new HashSet<string>();

        bool TryAddLink(Item item)
        {
            if (item.Link is not { } link)
                return false;

            var key = $"{link.Target}|{(int?)link.Properties.TargetType ?? -1}";
            if (!sentLinks.Add(key))
                return false;

            return true;
        }

        void AddTypeLinks(List<Item> links, ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol namedType)
                return;
            if (IgnoredTypes.IsIgnoredSystemType(namedType))
                return;
            if (SymbolEqualityComparer.Default.Equals(namedType, member.ContainingType))
                return;

            var item = LinkParser.Parse(namedType, fullMethodName);
            if (TryAddLink(item))
                links.Add(item);
        }

        void AddSymbolLink(List<Item> links, ISymbol? symbol)
        {
            if (symbol is null)
                return;

            if (symbol is IMethodSymbol methodSymbol)
            {
                if (methodSymbol.MethodKind == MethodKind.LocalFunction)
                    return;

                methodSymbol = methodSymbol.OriginalDefinition;

                if (methodSymbol.IsImplicitlyDeclared && methodSymbol.MethodKind == MethodKind.Constructor)
                    return;

                if (
                    methodSymbol.ContainingType is INamedTypeSymbol methodType
                    && IgnoredTypes.IsIgnoredSystemType(methodType)
                )
                    return;

                var methodItem = LinkParser.Parse(methodSymbol, fullMethodName);
                if (TryAddLink(methodItem))
                    links.Add(methodItem);

                AddTypeLinks(links, methodSymbol.ReturnType);
                foreach (var parameter in methodSymbol.Parameters)
                    AddTypeLinks(links, parameter.Type);
                foreach (var typeArg in methodSymbol.TypeArguments)
                    AddTypeLinks(links, typeArg);

                return;
            }

            if (symbol is IFieldSymbol fieldSymbol)
            {
                if (
                    fieldSymbol.ContainingType is INamedTypeSymbol fieldType
                    && IgnoredTypes.IsIgnoredSystemType(fieldType)
                )
                    return;

                AddTypeLinks(links, fieldSymbol.Type);

                var fieldItem = LinkParser.Parse(fieldSymbol, fullMethodName);
                if (TryAddLink(fieldItem))
                    links.Add(fieldItem);
            }
        }

        var links = new List<Item>();

        // Get link for method return type
        if (member.MethodKind is not (MethodKind.Constructor or MethodKind.StaticConstructor))
            AddTypeLinks(links, member.ReturnType);

        // Get links for method parameters
        foreach (var parameter in member.Parameters)
            AddTypeLinks(links, parameter.Type);

        // Parse links to method body field types/references and references to other types and methods
        foreach (var syntaxRef in member.DeclaringSyntaxReferences)
        {
            if (compilation is null)
                break;

            var syntax = syntaxRef.GetSyntax();
            IEnumerable<SyntaxNode> bodyNodes;
            if (syntax is BaseMethodDeclarationSyntax methodDeclaration)
                bodyNodes = GetMethodBodyNodes(methodDeclaration);
            else if (syntax is LocalFunctionStatementSyntax localFunction)
                bodyNodes = GetLocalFunctionBodyNodes(localFunction);
            else
                continue;

            var syntaxTree = syntax.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            foreach (var bodyNode in bodyNodes)
            {
                foreach (var node in bodyNode.DescendantNodesAndSelf())
                {
                    switch (node)
                    {
                        case VariableDeclarationSyntax variableDeclaration:
                            AddTypeLinks(links, semanticModel.GetTypeInfo(variableDeclaration.Type).Type);
                            break;
                        case ForEachStatementSyntax forEachStatement:
                            AddTypeLinks(links, semanticModel.GetTypeInfo(forEachStatement.Type).Type);
                            break;
                        case CatchDeclarationSyntax catchDeclaration when catchDeclaration.Type is not null:
                            AddTypeLinks(links, semanticModel.GetTypeInfo(catchDeclaration.Type).Type);
                            break;
                        case InvocationExpressionSyntax invocation:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(invocation).Symbol);
                            break;
                        case ObjectCreationExpressionSyntax objectCreation:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(objectCreation).Symbol);
                            break;
                        case ImplicitObjectCreationExpressionSyntax implicitObjectCreation:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(implicitObjectCreation).Symbol);
                            break;
                        case ConstructorInitializerSyntax constructorInitializer:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(constructorInitializer).Symbol);
                            break;
                        case IdentifierNameSyntax identifierName:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(identifierName).Symbol);
                            break;
                        case MemberAccessExpressionSyntax memberAccess:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(memberAccess).Symbol);
                            break;
                        case MemberBindingExpressionSyntax memberBinding:
                            AddSymbolLink(links, semanticModel.GetSymbolInfo(memberBinding).Symbol);
                            break;
                    }
                }
            }
        }

        foreach (var link in links)
            yield return link;
    }

    static IEnumerable<SyntaxNode> GetMethodBodyNodes(BaseMethodDeclarationSyntax methodDeclaration)
    {
        if (methodDeclaration.Body is not null)
            yield return methodDeclaration.Body;
        if (methodDeclaration.ExpressionBody is not null)
            yield return methodDeclaration.ExpressionBody.Expression;
    }

    static IEnumerable<SyntaxNode> GetLocalFunctionBodyNodes(LocalFunctionStatementSyntax localFunction)
    {
        if (localFunction.Body is not null)
            yield return localFunction.Body;
        if (localFunction.ExpressionBody is not null)
            yield return localFunction.ExpressionBody.Expression;
    }
}

class MemberParser
{
    public static IEnumerable<Item> ParseTypeMember(ISymbol member, string fullTypeName, Compilation compilation)
    {
        var items = member switch
        {
            IMethodSymbol m => ParseMethod(m, fullTypeName, compilation),
            IPropertySymbol p => ParseProperty(p, fullTypeName),
            IFieldSymbol f => ParseField(f, fullTypeName),
            IEventSymbol e => ParseEvent(e, fullTypeName),
            _ => throw new NotSupportedException($"Member type not supported: {member}"),
        };

        foreach (var item in items)
            yield return item;
    }

    static IEnumerable<Item> ParseEvent(IEventSymbol member, string fullTypeName)
    {
        yield return ParseMember(member, fullTypeName);
    }

    static IEnumerable<Item> ParseField(IFieldSymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        // Handle field link
        if (member.Type is INamedTypeSymbol fieldType && !IgnoredTypes.IsIgnoredSystemType(fieldType))
            yield return LinkParser.Parse(fieldType, memberNode.Node!.Name);
    }

    static IEnumerable<Item> ParseProperty(IPropertySymbol member, string fullTypeName)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        // Handle property link
        if (member.Type is INamedTypeSymbol fieldType && !IgnoredTypes.IsIgnoredSystemType(fieldType))
            yield return LinkParser.Parse(fieldType, memberNode.Node!.Name);
    }

    static IEnumerable<Item> ParseMethod(IMethodSymbol member, string fullTypeName, Compilation compilation)
    {
        var memberNode = ParseMember(member, fullTypeName);
        yield return memberNode;

        foreach (var item in MethodParser.ParseMethodLinks(member, memberNode.Node!.Name, compilation))
            yield return item;
    }

    static Item ParseMember(ISymbol member, string fullTypeName)
    {
        var name = Names.GetFullMemberName(member, fullTypeName);
        var fileSpan = Locations.GetFirstFileSpanOrNoValue(member);
        var leadingComment = CommentExtractor.GetLeadingCommentOrNoValue(member, fileSpan);
        var nodeType = NodeTypes.ToTypes(member);
        var isPrivate = SymbolUtils.GetIsPrivate(member);

        return new Item(
            new Node(
                name,
                new NodeProperties
                {
                    Type = nodeType,
                    Description = leadingComment,
                    FileSpan = fileSpan,
                    IsPrivate = isPrivate,
                }
            ),
            null
        );
    }
}

internal class IgnoredTypes
{
    public static bool IsIgnoredSystemType(INamedTypeSymbol type)
    {
        if (type.ContainingModule?.Name == "System.Runtime.dll")
            return true;

        return false;
        // if (type.FullName.StartsWith("__Blazor"))
        //     return true;

        // return IsSystemIgnoredModuleName(targetType.Scope.Name);
    }

    public static bool IsSystemIgnoredModuleName(string moduleName)
    {
        return moduleName == "mscorlib"
            || moduleName == "PresentationFramework"
            || moduleName == "PresentationCore"
            || moduleName == "WindowsBase"
            || moduleName == "System"
            || moduleName.StartsWith("Microsoft.")
            || moduleName.StartsWith("System.");
    }
}
