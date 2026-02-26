using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependinatorRoslyn.Parsing;

class MethodLinkParser
{
    public static IEnumerable<Link> ParseMethodLinks(
        IMethodSymbol member,
        string fullMethodName,
        Compilation compilation
    )
    {
        // Get link for method return type
        if (member.MethodKind is not (MethodKind.Constructor or MethodKind.StaticConstructor))
            foreach (var link in AddTypeLinks(member.ReturnType, member, fullMethodName))
                yield return link;

        // Get links for method parameters
        foreach (var parameter in member.Parameters)
        {
            foreach (var link in AddTypeLinks(parameter.Type, member, fullMethodName))
                yield return link;
        }

        // Parse links to method body field types/references and references to other types and methods
        foreach (var link in ParseBodyLinks(member, fullMethodName, compilation))
            yield return link;
    }

    private static IEnumerable<Link> ParseBodyLinks(
        IMethodSymbol member,
        string fullMethodName,
        Compilation compilation
    )
    {
        foreach (var syntaxRef in member.DeclaringSyntaxReferences)
        {
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
                    IEnumerable<Link> bodyLinks = ParseSyntaxNodeLinks(node, member, fullMethodName, semanticModel);
                    foreach (var bodyLink in bodyLinks)
                        yield return bodyLink;
                }
            }
        }
    }

    static IEnumerable<Link> ParseSyntaxNodeLinks(
        SyntaxNode node,
        IMethodSymbol member,
        string fullMethodName,
        SemanticModel semanticModel
    )
    {
        return node switch
        {
            VariableDeclarationSyntax variableDeclaration => AddTypeLinks(
                semanticModel.GetTypeInfo(variableDeclaration.Type).Type,
                member,
                fullMethodName
            ),

            ForEachStatementSyntax forEachStatement => AddTypeLinks(
                semanticModel.GetTypeInfo(forEachStatement.Type).Type,
                member,
                fullMethodName
            ),

            CatchDeclarationSyntax catchDeclaration when catchDeclaration.Type is not null => AddTypeLinks(
                semanticModel.GetTypeInfo(catchDeclaration.Type).Type,
                member,
                fullMethodName
            ),

            InvocationExpressionSyntax invocation => AddSymbolLink(
                semanticModel.GetSymbolInfo(invocation).Symbol,
                member,
                fullMethodName
            ),

            ObjectCreationExpressionSyntax objectCreation => AddSymbolLink(
                semanticModel.GetSymbolInfo(objectCreation).Symbol,
                member,
                fullMethodName
            ),

            ImplicitObjectCreationExpressionSyntax implicitObjectCreation => AddSymbolLink(
                semanticModel.GetSymbolInfo(implicitObjectCreation).Symbol,
                member,
                fullMethodName
            ),

            ConstructorInitializerSyntax constructorInitializer => AddSymbolLink(
                semanticModel.GetSymbolInfo(constructorInitializer).Symbol,
                member,
                fullMethodName
            ),

            IdentifierNameSyntax identifierName => AddSymbolLink(
                semanticModel.GetSymbolInfo(identifierName).Symbol,
                member,
                fullMethodName
            ),

            MemberAccessExpressionSyntax memberAccess => AddSymbolLink(
                semanticModel.GetSymbolInfo(memberAccess).Symbol,
                member,
                fullMethodName
            ),

            MemberBindingExpressionSyntax memberBinding => AddSymbolLink(
                semanticModel.GetSymbolInfo(memberBinding).Symbol,
                member,
                fullMethodName
            ),
            _ => [],
        };
    }

    static IEnumerable<Link> AddTypeLinks(ITypeSymbol? type, IMethodSymbol member, string fullMethodName)
    {
        if (
            type is not INamedTypeSymbol namedType
            || IgnoredTypes.IsIgnoredSystemType(namedType)
            || SymbolEqualityComparer.Default.Equals(namedType, member.ContainingType)
        )
            yield break;

        yield return LinkParser.Parse(fullMethodName, namedType);
    }

    static IEnumerable<Link> AddSymbolLink(ISymbol? symbol, IMethodSymbol member, string fullMethodName)
    {
        if (symbol is null)
            yield break;

        if (symbol is IFieldSymbol fieldSymbol) // Method field
        {
            if (fieldSymbol.ContainingType is INamedTypeSymbol fieldType && IgnoredTypes.IsIgnoredSystemType(fieldType))
                yield break;

            foreach (var fieldTypeLink in AddTypeLinks(fieldSymbol.Type, member, fullMethodName))
                yield return fieldTypeLink;

            var fieldItem = LinkParser.Parse(fullMethodName, fieldSymbol);
            yield return fieldItem;
        }

        if (symbol is IMethodSymbol methodSymbol) // Method call to other method
        {
            if (methodSymbol.MethodKind == MethodKind.LocalFunction)
                yield break;

            methodSymbol = methodSymbol.OriginalDefinition;

            if (methodSymbol.IsImplicitlyDeclared && methodSymbol.MethodKind == MethodKind.Constructor)
                yield break;
            if (
                methodSymbol.ContainingType is INamedTypeSymbol methodType
                && IgnoredTypes.IsIgnoredSystemType(methodType)
            )
                yield break;

            var methodItem = LinkParser.Parse(fullMethodName, methodSymbol);
            yield return methodItem;

            foreach (var returnTypeLink in AddTypeLinks(methodSymbol.ReturnType, member, fullMethodName))
                yield return returnTypeLink;

            foreach (var parameter in methodSymbol.Parameters)
            {
                foreach (var parameterTypeLink in AddTypeLinks(parameter.Type, member, fullMethodName))
                    yield return parameterTypeLink;
            }

            foreach (var genericTypeArg in methodSymbol.TypeArguments)
            {
                foreach (var genericTypeLink in AddTypeLinks(genericTypeArg, member, fullMethodName))
                    yield return genericTypeLink;
            }
        }
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
