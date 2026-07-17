using Dependinator.UI.App;

namespace Dependinator.UI.Tests.App;

public class NewModelNameValidatorTests
{
    static readonly string[] ExistingPaths = ["/workspaces/Dependinator/Dependinator.sln", "My Design"];

    [Fact]
    public void Validate_ShouldAccept_ValidNewName()
    {
        Assert.Null(NewModelNameValidator.Validate("Cloud Design", ExistingPaths));
        Assert.Null(NewModelNameValidator.Validate("  Cloud Design  ", ExistingPaths));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldReject_EmptyName(string name)
    {
        Assert.NotNull(NewModelNameValidator.Validate(name, ExistingPaths));
    }

    [Theory]
    [InlineData("My/Design")]
    [InlineData("My\\Design")]
    public void Validate_ShouldReject_PathSeparators(string name)
    {
        Assert.NotNull(NewModelNameValidator.Validate(name, ExistingPaths));
    }

    [Theory]
    [InlineData("MyModel.sln")]
    [InlineData("MyModel.SLN")]
    public void Validate_ShouldReject_SolutionSuffix(string name)
    {
        Assert.NotNull(NewModelNameValidator.Validate(name, ExistingPaths));
    }

    [Theory]
    [InlineData("My Design")]
    [InlineData("my design")] // Same cloud blob key as "My Design"
    public void Validate_ShouldReject_NameOfExistingModel(string name)
    {
        Assert.NotNull(NewModelNameValidator.Validate(name, ExistingPaths));
    }

    [Fact]
    public void Validate_ShouldReject_NameWithoutValidKeyCharacters()
    {
        Assert.NotNull(NewModelNameValidator.Validate("...", ExistingPaths));
    }
}
