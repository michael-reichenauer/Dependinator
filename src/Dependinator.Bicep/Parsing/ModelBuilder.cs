using System.Collections.Immutable;
using Bicep.Core.Emit;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Metadata;
using Bicep.Core.Syntax;
using Bicep.Core.Syntax.Visitors;
using Bicep.IO.Abstraction;

namespace Dependinator.Bicep.Parsing;

// Walks a compiled Bicep deployment into model items.
//
// Hierarchy (dotted node names, see Names):
//   main*bicep                      the entry file (NodeType.Solution, icon of the target scope)
//   main*bicep.location             a parameter / variable / output (member rows)
//   main*bicep.web                  a resource (NodeType.Resource, Azure icon of its type)
//   main*bicep.web.config           a child resource (nested syntax or "parent:" property)
//   main*bicep.web.diagnostics      an extension resource "scope:"d to a resource deployed in the
//                                   same file nests under it (so do modules deployed into a
//                                   resource group the file creates: main*bicep.rgData.storage);
//                                   an auxiliary leaf resource (AuxiliaryResources) is a row
//   main*bicep.storage              a module (NodeType.Module); a local module's own file is
//   main*bicep.storage.account      expanded beneath it, recursively, and the module shows the
//                                   icon of its principal resource (or of "metadata icon = '...'")
//   $Externals.vnet                 an "existing" resource: deployed elsewhere, only referenced
//
// Links come from the compiler's own dependency graph (dependsOn, parent, scope, property
// reads) plus the symbols each declaration references (parameters, variables, module outputs).
class ModelBuilder(string entryPath)
{
    readonly List<Item> nodeItems = [];
    readonly Dictionary<string, int> nodeIndex = [];
    readonly Dictionary<string, NodeType> nodeTypes = [];
    readonly List<(string Source, string Target)> links = [];

    // Output nodes by (module node, output identifier): an output may share its identifier with a
    // parameter or variable of the same file (outputs are not referenceable), so its node name
    // can differ from the identifier and is looked up here when another file reads it.
    readonly Dictionary<(string Module, string Output), string> outputNodes = [];
    readonly HashSet<(string Source, string Target)> linkSet = [];

    public IReadOnlyList<Item> Build(SemanticModel model)
    {
        var rootName = Names.GetRootName(entryPath);
        var description =
            DescriptionHelper.TryGetFromSemanticModel(model) ?? $"Bicep deployment, target scope {model.TargetScope}";
        AddNode(
            rootName,
            NodeType.Solution,
            description,
            Locations.ToFileSpan(model, model.SourceFile.ProgramSyntax),
            iconName: MetadataIcon(model) ?? AzureIcons.ForScope(model.TargetScope)
        );

        AddFile(model, rootName, modulePath: "", visited: [model.SourceFile.FileHandle.Uri]);

        return [.. nodeItems, .. LinkItems()];
    }

    // Adds one Bicep file's declarations under prefix; modulePath is the module chain below the
    // root ("" for the entry file, "storage" or "storage.inner" for expanded modules) used to
    // group that file's existing resources under $Externals.
    void AddFile(SemanticModel model, string prefix, string modulePath, ImmutableHashSet<IOUri> visited)
    {
        var names = new Dictionary<DeclaredSymbol, string>(ReferenceEqualityComparer.Instance);
        var externalsPrefix = modulePath == "" ? Names.ExternalsName : Names.Child(Names.ExternalsName, modulePath);

        foreach (var parameter in model.Root.ParameterDeclarations)
        {
            names[parameter] = Names.Child(prefix, parameter.Name);
            AddNode(
                names[parameter],
                NodeType.ParameterMember,
                Describe(model, parameter, $"param {parameter.Type.Name}"),
                Locations.ToFileSpan(model, parameter.DeclaringSyntax)
            );
        }

        foreach (var variable in model.Root.VariableDeclarations)
        {
            names[variable] = Names.Child(prefix, variable.Name);
            AddNode(
                names[variable],
                NodeType.VariableMember,
                Describe(model, variable, "var"),
                Locations.ToFileSpan(model, variable.DeclaringSyntax),
                isPrivate: true
            );
        }

        // Outputs live outside the file's identifier scope, so one may be named like a parameter,
        // variable, resource or module; such an output gets a distinguishing suffix.
        var takenNames = new HashSet<string>(StringComparer.Ordinal);
        takenNames.UnionWith(model.Root.ParameterDeclarations.Select(p => p.Name));
        takenNames.UnionWith(model.Root.VariableDeclarations.Select(v => v.Name));
        takenNames.UnionWith(model.Root.ResourceDeclarations.Select(r => r.Name));
        takenNames.UnionWith(model.Root.ModuleDeclarations.Select(m => m.Name));
        foreach (var output in model.Root.OutputDeclarations)
        {
            var leafName = takenNames.Contains(output.Name) ? $"{output.Name} (output)" : output.Name;
            names[output] = Names.Child(prefix, leafName);
            outputNodes[(prefix, output.Name)] = names[output];
            AddNode(
                names[output],
                NodeType.OutputMember,
                Describe(model, output, $"output {output.Type.Name}"),
                Locations.ToFileSpan(model, output.DeclaringSyntax)
            );
        }

        // Resources: name every one first (a child declared before its parent still nests, and a
        // resource "scope:"d to another nests under it), then emit parents before children so the
        // structure never needs synthesized placeholders.
        var byResource = new Dictionary<ResourceSymbol, DeclaredResourceMetadata>(ReferenceEqualityComparer.Instance);
        foreach (var resource in model.DeclaredResources)
            byResource[resource.Symbol] = resource;
        var finalNames = new Dictionary<DeclaredResourceMetadata, string>(ReferenceEqualityComparer.Instance);
        var resolving = new HashSet<DeclaredResourceMetadata>(ReferenceEqualityComparer.Instance);
        string FinalName(DeclaredResourceMetadata resource)
        {
            if (finalNames.TryGetValue(resource, out var known))
                return known;
            // The guard only matters for a scope cycle, which the compiler rejects anyway.
            var scopeTarget = resolving.Add(resource) ? ScopeTarget(model, resource, byResource) : null;
            var name = scopeTarget is not null
                ? Names.Child(FinalName(scopeTarget), resource.Symbol.Name)
                : ResourceName(model, resource, prefix, externalsPrefix);
            resolving.Remove(resource);
            return finalNames[resource] = name;
        }

        var resources = model.DeclaredResources.Select(r => (Resource: r, Name: FinalName(r))).ToList();
        foreach (var (resource, name) in resources.OrderBy(r => r.Name.Count(c => c == '.')).ThenBy(r => r.Name))
        {
            names[resource.Symbol] = name;
            var typeReference = resource.TypeReference;
            // An auxiliary resource (a setting, assignment or lock) without children of its own
            // is a row under the resource it configures, not a box next to it.
            var nodeType =
                !resource.IsExistingResource
                && AuxiliaryResources.IsAuxiliary(typeReference.FormatType())
                && !resources.Any(r => r.Name.StartsWith(name + "."))
                    ? NodeType.ResourceMember
                    : NodeType.Resource;
            var iconName = resource.IsAzResource
                ? AzureIcons.ForResourceType(typeReference.FormatType())
                : AzureIcons.Fallback;
            AddNode(
                name,
                nodeType,
                Describe(model, resource.Symbol, typeReference.FormatName() + ResourceMarkers(resource)),
                Locations.ToFileSpan(model, resource.Symbol.DeclaringSyntax),
                iconName: iconName
            );
        }

        // A module deployed into a resource group this file creates ("scope: rgData") nests under
        // that resource group node; every resource is named by now.
        foreach (var module in model.Root.ModuleDeclarations)
        {
            var scopeTarget = ScopeTarget(model, module.DeclaringModule.TryGetBody(), byResource);
            var parentName = scopeTarget is not null ? names[scopeTarget.Symbol] : prefix;
            names[module] = Names.Child(parentName, module.Name);
            AddModule(model, module, names[module], modulePath, visited);
        }

        AddLinks(model, names);
    }

    // The resource a declaration is "scope:"d to, when that is a resource deployed in the same
    // file (an extension resource on one of the file's own resources, a module deployed into a
    // resource group the file creates). A function scope such as resourceGroup('x') resolves to
    // no symbol and an existing resource lives under $Externals: neither is nested under.
    static DeclaredResourceMetadata? ScopeTarget(
        SemanticModel model,
        DeclaredResourceMetadata resource,
        Dictionary<ResourceSymbol, DeclaredResourceMetadata> byResource
    ) =>
        resource.IsExistingResource
            ? null
            : ScopeTarget(model, resource.Symbol.DeclaringResource.TryGetBody(), byResource);

    static DeclaredResourceMetadata? ScopeTarget(
        SemanticModel model,
        ObjectSyntax? body,
        Dictionary<ResourceSymbol, DeclaredResourceMetadata> byResource
    )
    {
        var scope = body?.Properties.FirstOrDefault(p => p.TryGetKeyText() == "scope")?.Value;
        return
            scope is not null
            && model.GetSymbolInfo(scope) is ResourceSymbol symbol
            && byResource.TryGetValue(symbol, out var target)
            && !target.IsExistingResource
            ? target
            : null;
    }

    // The node name of a resource from its ancestor chain. An "existing" resource (deployed
    // elsewhere) goes under $Externals; a deployed resource nests under its deployed ancestors
    // only, so a child of an existing parent sits in the deployment and links to the parent.
    static string ResourceName(
        SemanticModel model,
        DeclaredResourceMetadata resource,
        string prefix,
        string externalsPrefix
    )
    {
        var ancestors = model.ResourceAncestors.GetAncestors(resource).Select(a => a.Resource).ToList();
        IEnumerable<DeclaredResourceMetadata> chain;
        string basePrefix;
        if (resource.IsExistingResource)
        {
            basePrefix = externalsPrefix;
            chain = ancestors.Where(a => a.IsExistingResource);
        }
        else
        {
            basePrefix = prefix;
            var lastExisting = ancestors.FindLastIndex(a => a.IsExistingResource);
            chain = ancestors.Skip(lastExisting + 1);
        }

        var name = basePrefix;
        foreach (var ancestor in chain)
            name = Names.Child(name, ancestor.Symbol.Name);
        return Names.Child(name, resource.Symbol.Name);
    }

    static string ResourceMarkers(DeclaredResourceMetadata resource)
    {
        var declaration = resource.Symbol.DeclaringResource;
        var markers = new List<string>();
        if (resource.IsExistingResource)
            markers.Add("existing");
        if (declaration.IsCollection())
            markers.Add("loop");
        if (declaration.HasCondition())
            markers.Add("conditional");
        return markers.Count == 0 ? "" : $" ({string.Join(", ", markers)})";
    }

    void AddModule(
        SemanticModel model,
        ModuleSymbol module,
        string moduleName,
        string modulePath,
        ImmutableHashSet<IOUri> visited
    )
    {
        var declaration = (ModuleDeclarationSyntax)module.DeclaringSyntax;
        var pathText = (declaration.Path as StringSyntax)?.TryGetLiteralValue() ?? declaration.Path.ToString();
        var markers = new List<string>();
        if (declaration.Value is ForSyntax)
            markers.Add("loop");
        if (declaration.HasCondition())
            markers.Add("conditional");
        var markerText = markers.Count == 0 ? "" : $" ({string.Join(", ", markers)})";

        var span = Locations.ToFileSpan(model, module.DeclaringSyntax);
        var childModulePath = modulePath == "" ? module.Name : Names.Child(modulePath, module.Name);

        if (!module.TryGetSemanticModel().IsSuccess(out var referenced, out var failure))
        {
            // Registry (br:/ts:) modules are not restored (no network, no credentials) and a
            // missing local file cannot be read; both stay as leaf modules.
            var reason = failure?.Message ?? "unknown";
            AddNode(
                moduleName,
                NodeType.Module,
                Describe(model, module, $"module {pathText}{markerText}, not expanded: {reason}"),
                span
            );
            return;
        }

        if (referenced is not SemanticModel moduleModel)
        {
            // ARM JSON templates and template specs are not walked into (yet).
            AddNode(
                moduleName,
                NodeType.Module,
                Describe(model, module, $"module {pathText}{markerText}, ARM template"),
                span
            );
            return;
        }

        var moduleUri = moduleModel.SourceFile.FileHandle.Uri;
        if (visited.Contains(moduleUri))
        {
            AddNode(
                moduleName,
                NodeType.Module,
                Describe(model, module, $"module {pathText}{markerText}, recursive"),
                span
            );
            return;
        }

        var description =
            DescriptionHelper.TryGetFromDecorator(model, declaration)
            ?? DescriptionHelper.TryGetFromSemanticModel(moduleModel)
            ?? $"module {pathText}{markerText}";
        AddNode(moduleName, NodeType.Module, description, span, iconName: MetadataIcon(moduleModel));
        AddFile(moduleModel, moduleName, childModulePath, visited.Add(moduleUri));

        // Without a declared icon the module shows what it mainly deploys, known once its file is
        // walked (the principal is picked by the links inside the module).
        if (IconOf(moduleName) is null && PrincipalResourceIcon(moduleName) is { } icon)
            SetIconName(moduleName, icon);
    }

    // "metadata icon = 'Key-Vault'" at the top of a file picks its node's icon: an id from the
    // UI's icon library (an unknown id falls back to the node type's default icon).
    static string? MetadataIcon(SemanticModel model) =>
        model.Root.MetadataDeclarations.FirstOrDefault(m => m.Name == "icon")?.DeclaringSyntax
            is MetadataDeclarationSyntax { Value: StringSyntax value }
            ? value.TryGetLiteralValue()
            : null;

    // The icon of a module's principal resource: the direct child resource with the most incoming
    // links inside the module (outputs, sibling references and module parameters point at it),
    // then the most descendants, then the first emitted. A module made only of modules takes its
    // first module's icon; a module without either keeps the default module icon.
    string? PrincipalResourceIcon(string moduleName)
    {
        var prefix = moduleName + ".";
        var children = nodeTypes
            .Where(kv => kv.Key.StartsWith(prefix) && !kv.Key.AsSpan(prefix.Length).Contains('.'))
            .ToList();
        var principal =
            children
                .Where(c => c.Value == NodeType.Resource)
                .OrderByDescending(c => links.Count(l => l.Target == c.Key))
                .ThenByDescending(c => nodeTypes.Keys.Count(k => k.StartsWith(c.Key + ".")))
                .ThenBy(c => nodeIndex[c.Key])
                .Select(c => c.Key)
                .FirstOrDefault()
            ?? children
                .Where(c => c.Value == NodeType.Module)
                .OrderBy(c => nodeIndex[c.Key])
                .Select(c => c.Key)
                .FirstOrDefault();
        return principal is null ? null : IconOf(principal);
    }

    string? IconOf(string name) => nodeItems[nodeIndex[name]].Node!.Properties.IconName;

    void SetIconName(string name, string iconName)
    {
        var properties = nodeItems[nodeIndex[name]].Node!.Properties;
        nodeItems[nodeIndex[name]] = new Item(
            new Node(
                name,
                new NodeProperties
                {
                    Type = properties.Type,
                    Description = properties.Description,
                    FileSpan = properties.FileSpan,
                    IconName = iconName,
                    IsPrivate = properties.IsPrivate,
                }
            ),
            null
        );
    }

    void AddLinks(SemanticModel model, Dictionary<DeclaredSymbol, string> names)
    {
        // The compiler's deployment dependency graph: dependsOn, parent/scope properties and
        // every property read of another resource or module (also through variables).
        foreach (var (symbol, dependencies) in ResourceDependencyVisitor.GetResourceDependencies(model))
        {
            if (!names.TryGetValue(symbol, out var source))
                continue;
            foreach (var dependency in dependencies)
            {
                if (names.TryGetValue(dependency.Resource, out var target))
                    AddLink(source, target);
            }
        }

        // Everything a declaration references: parameters and variables it reads, the resources
        // and modules an output exposes, the values a module is given. A resource's nested child
        // declarations are walked for the child, not for the parent.
        foreach (var (symbol, source) in names)
        {
            var references = DeclarationReferences.Collect(symbol.DeclaringSyntax);
            foreach (var reference in references.SymbolReferences)
            {
                if (
                    model.GetSymbolInfo(reference) is DeclaredSymbol referenced
                    && names.TryGetValue(referenced, out var target)
                )
                    AddLink(source, target);
            }

            AddModuleOutputLinks(model, references.PropertyAccesses, source, names);
        }

        foreach (var module in model.Root.ModuleDeclarations)
            AddModuleParameterLinks(model, module, names);
    }

    // "storage.outputs.accountName" read by a declaration links it to that output node when the
    // module was expanded (the plain link to the module is dropped in LinkItems).
    void AddModuleOutputLinks(
        SemanticModel model,
        IEnumerable<PropertyAccessSyntax> propertyAccesses,
        string source,
        Dictionary<DeclaredSymbol, string> names
    )
    {
        foreach (var access in propertyAccesses)
        {
            if (
                access.BaseExpression is PropertyAccessSyntax { PropertyName.IdentifierName: "outputs" } outputs
                && model.GetSymbolInfo(outputs.BaseExpression) is ModuleSymbol module
                && names.TryGetValue(module, out var moduleName)
            )
            {
                if (outputNodes.TryGetValue((moduleName, access.PropertyName.IdentifierName), out var outputName))
                    AddLink(source, outputName);
            }
        }
    }

    // "params: { location: location }" links the module's own parameter node (when the module
    // was expanded) to the value's symbol, instead of the whole module to it. A value read from
    // another module's output ("storage.outputs.accountName") links to that output node.
    void AddModuleParameterLinks(SemanticModel model, ModuleSymbol module, Dictionary<DeclaredSymbol, string> names)
    {
        if (!names.TryGetValue(module, out var moduleName))
            return;
        var declaration = (ModuleDeclarationSyntax)module.DeclaringSyntax;
        var parameters =
            declaration.TryGetBody()?.Properties.FirstOrDefault(p => p.TryGetKeyText() == "params")?.Value
            as ObjectSyntax;
        if (parameters is null)
            return;

        foreach (var property in parameters.Properties)
        {
            if (property.TryGetKeyText() is not { } key)
                continue;
            var parameterName = Names.Child(moduleName, key);
            if (!nodeTypes.ContainsKey(parameterName))
                continue;

            var modulesLinkedByOutput = new HashSet<ModuleSymbol>();
            foreach (var access in SyntaxAggregator.AggregateByType<PropertyAccessSyntax>(property.Value))
            {
                if (
                    access.BaseExpression is PropertyAccessSyntax { PropertyName.IdentifierName: "outputs" } outputs
                    && model.GetSymbolInfo(outputs.BaseExpression) is ModuleSymbol sourceModule
                    && names.TryGetValue(sourceModule, out var sourceModuleName)
                    && outputNodes.TryGetValue(
                        (sourceModuleName, access.PropertyName.IdentifierName),
                        out var outputName
                    )
                )
                {
                    AddLink(parameterName, outputName);
                    modulesLinkedByOutput.Add(sourceModule);
                }
            }

            foreach (var access in SyntaxAggregator.AggregateByType<VariableAccessSyntax>(property.Value))
            {
                if (
                    model.GetSymbolInfo(access) is DeclaredSymbol referenced
                    && names.TryGetValue(referenced, out var target)
                    && !(
                        referenced is ModuleSymbol referencedModule && modulesLinkedByOutput.Contains(referencedModule)
                    )
                )
                    AddLink(parameterName, target);
            }
        }
    }

    static string Describe(SemanticModel model, DeclaredSymbol symbol, string defaultText) =>
        symbol.DeclaringSyntax is DecorableSyntax decorable
        && DescriptionHelper.TryGetFromDecorator(model, decorable) is { } text
            ? $"{text} ({defaultText})"
            : defaultText;

    void AddNode(
        string name,
        NodeType type,
        string description,
        FileSpan fileSpan,
        string? iconName = null,
        bool? isPrivate = null
    )
    {
        nodeTypes[name] = type;
        nodeIndex[name] = nodeItems.Count;
        nodeItems.Add(
            new Item(
                new Node(
                    name,
                    new NodeProperties
                    {
                        Type = type,
                        Description = description,
                        FileSpan = fileSpan,
                        IconName = iconName,
                        IsPrivate = isPrivate,
                    }
                ),
                null
            )
        );
    }

    void AddLink(string source, string target)
    {
        // A link to a containing node (a child resource to its parent, a module's content to the
        // module) is the nesting itself, not a dependency.
        if (source == target || source.StartsWith(target + ".") || target.StartsWith(source + "."))
            return;
        if (linkSet.Add((source, target)))
            links.Add((source, target));
    }

    // A link to a module is dropped when a finer link into that module exists (to one of its
    // outputs, or from one of its parameters), so the diagram shows the actual value flow.
    IEnumerable<Item> LinkItems()
    {
        foreach (var (source, target) in links)
        {
            if (
                nodeTypes.GetValueOrDefault(target) == NodeType.Module
                && links.Any(l => l.Source == source && l.Target.StartsWith(target + "."))
            )
                continue;
            if (
                nodeTypes.GetValueOrDefault(source) == NodeType.Module
                && links.Any(l => l.Target == target && l.Source.StartsWith(source + "."))
            )
                continue;
            NodeType? targetType = nodeTypes.TryGetValue(target, out var type) ? type : null;
            // A value read from a parameter or variable is configuration plumbing (hidden by
            // default in the diagram); a module parameter fed by another module's output, a
            // resource reference or an output's exposure is the deployment's real wiring.
            bool? isConfiguration = targetType is NodeType.ParameterMember or NodeType.VariableMember ? true : null;
            yield return new Item(
                null,
                new Link(
                    source,
                    target,
                    new LinkProperties { TargetType = targetType, IsConfiguration = isConfiguration }
                )
            );
        }
    }

    // The symbol references ("location", "plan.id", "vnet::subnet") and property accesses inside one
    // declaration, not descending into nested resource declarations, which are declarations of
    // their own.
    sealed class DeclarationReferences : AstVisitor
    {
        SyntaxBase? root;
        public List<SyntaxBase> SymbolReferences { get; } = [];
        public List<PropertyAccessSyntax> PropertyAccesses { get; } = [];

        public static DeclarationReferences Collect(SyntaxBase declaration)
        {
            var references = new DeclarationReferences { root = declaration };
            references.Visit(declaration);
            return references;
        }

        public override void VisitResourceDeclarationSyntax(ResourceDeclarationSyntax syntax)
        {
            if (ReferenceEquals(syntax, root))
                base.VisitResourceDeclarationSyntax(syntax);
        }

        public override void VisitVariableAccessSyntax(VariableAccessSyntax syntax)
        {
            SymbolReferences.Add(syntax);
            base.VisitVariableAccessSyntax(syntax);
        }

        public override void VisitResourceAccessSyntax(ResourceAccessSyntax syntax)
        {
            SymbolReferences.Add(syntax);
            base.VisitResourceAccessSyntax(syntax);
        }

        public override void VisitPropertyAccessSyntax(PropertyAccessSyntax syntax)
        {
            PropertyAccesses.Add(syntax);
            base.VisitPropertyAccessSyntax(syntax);
        }
    }
}
