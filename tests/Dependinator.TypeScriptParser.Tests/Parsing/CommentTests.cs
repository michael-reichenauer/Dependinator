using Dependinator.Core.Parsing;
using Dependinator.TypeScriptParser.Tests.Parsing.Utils;

namespace Dependinator.TypeScriptParser.Tests.Parsing;

// The descriptions of declarations, from Samples/shop/src/notes.ts and Samples/tool/lib/notes.js:
// JSDoc, or else the plain comment lines directly above a declaration, the way the C# parser
// reads a declaration's comment.
[Collection(nameof(TypeScriptCollection))]
public class CommentTests(TypeScriptFixture fixture)
{
    const string Notes = "shop.src.notes*ts";

    string? Description(string name) => fixture.Shop.Node(name).Properties.Description;

    [Fact]
    public void Declarations_ShouldBeDescribedByTheLineCommentsAboveThem()
    {
        Assert.Equal("The longest note that is kept\n(longer ones are cut).", Description($"{Notes}.maxLength"));
        Assert.Equal("Formats notes for display.", Description($"{Notes}.Formatter"));
        Assert.Equal("What a stored note holds.", Description($"{Notes}.Note"));
        Assert.Equal("How important a note is.", Description($"{Notes}.Priority"));
    }

    [Fact]
    public void Members_ShouldBeDescribedByTheCommentsAboveThem()
    {
        Assert.Equal(
            "Makes a formatter that puts the prefix before each note.",
            Description($"{Notes}.Formatter.constructor")
        );
        Assert.Equal("The notes as lines of text.", Description($"{Notes}.Formatter.format"));
        Assert.Equal("The number of notes.", Description($"{Notes}.Formatter.count"));
        Assert.Equal("When it was written.", Description($"{Notes}.Note.time"));
        Assert.Equal("Can wait.", Description($"{Notes}.Priority.Low"));
    }

    [Fact]
    public void BlockComment_ShouldDescribeTheDeclarationBelowIt()
    {
        Assert.Equal("The notes, newest first", Description($"{Notes}.notes"));
    }

    [Fact]
    public void ToolDirectives_ShouldBeLeftOut()
    {
        Assert.Equal("Adds a note.", Description($"{Notes}.add"));
    }

    [Fact]
    public void CommentLines_ShouldDescribeADeclaration_WhenItsJSDocHasOnlyTags()
    {
        Assert.Equal("Kept for old callers.", Description($"{Notes}.old"));
    }

    [Fact]
    public void Declarations_ShouldHaveNoValue_WithoutACommentDirectlyAboveThem()
    {
        // NoValue, not null: a description whose comment was removed is cleared on a refresh
        Assert.Equal(NoValue.String, Description($"{Notes}.clear"));
        Assert.Equal(NoValue.String, Description($"{Notes}.minLength"));
        Assert.Equal(NoValue.String, Description($"{Notes}.Priority.High"));
        // A comment at the end of the line above belongs to that line
        Assert.Equal(NoValue.String, Description($"{Notes}.Note.text"));
        Assert.Equal(NoValue.String, Description($"{Notes}.Note.tags"));
    }

    [Fact]
    public void FilesTopComment_ShouldDescribeTheFile_NotItsFirstDeclaration()
    {
        Assert.Equal(
            "The shop's notes: the comment forms that describe a declaration (see CommentTests).",
            Description(Notes)
        );
    }

    [Fact]
    public void CommonJsExport_ShouldBeDescribedByTheCommentAboveItsAssignment()
    {
        Assert.Equal("Stops the tool.", fixture.Tool.Node("tool.lib.notes*js.stop").Properties.Description);
    }

    [Fact]
    public void LicenseHeader_ShouldNotDescribeTheFirstDeclaration()
    {
        Assert.Equal(NoValue.String, fixture.Tool.Node("tool.lib.notes*js.list").Properties.Description);
    }
}
