namespace Dependinator.Core.Parsing;

// The icons of a source's root node: a tile in the language's color with its letters, so the
// sources of a workspace model tell their language apart (IconLibrary ids, the UI's
// Diagrams/Icons/Library/Languages, made by scripts/language-icons/generate.py). Each parser sets
// its root's NodeProperties.IconName to one; the TypeScript parser's Node.js helper spells its two
// in its own code (NodeParser/src/model.ts).
static class LanguageIcons
{
    public const string CSharp = "CSharp";
    public const string TypeScript = "TypeScript";
    public const string JavaScript = "JavaScript";
    public const string Bicep = "Bicep";
    public const string Go = "Go";
}
