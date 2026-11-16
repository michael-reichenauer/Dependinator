using System.Text.Json;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.VsCode;

namespace Dependinator.UI.Tests.Shared;

// The Settings dialog's VS Code section reads and writes the extension's settings through
// "vscode/GetSettings", "vscode/SetSetting" and the "ui/vsCodeSettings" answer.
public class VsCodeSettingsServiceTests
{
    const string SettingsJson =
        """{"followActiveEditor":false,"autoRefreshEnabled":true,"autoRefreshDelaySeconds":5}""";

    readonly List<(string Type, string Message)> posted = [];

    VsCodeSettingsService CreateSut(bool isVsCode = true, Action<VsCodeSettingsService>? onPost = null)
    {
        Mock<IJSInterop> jSInterop = new();
        VsCodeSettingsService? sut = null;
        jSInterop
            .Setup(j => j.Call<bool>("postVsCodeMessage", It.IsAny<object?[]?>()))
            .Returns(
                (string _, object?[]? args) =>
                {
                    var json = JsonSerializer.SerializeToElement(args![0]);
                    posted.Add((json.GetProperty("type").GetString()!, json.GetProperty("message").GetString()!));
                    onPost?.Invoke(sut!);
                    return ValueTask.FromResult(isVsCode);
                }
            );
        sut = new VsCodeSettingsService(jSInterop.Object);
        return sut;
    }

    [Fact]
    public void Received_ShouldSetSettingsAndRaiseChanged()
    {
        var sut = CreateSut();
        int changedCount = 0;
        sut.Changed += () => changedCount++;

        sut.Received(SettingsJson);

        Assert.Equal(new VsCodeSettings(false, true, 5), sut.Settings);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void Received_ShouldIgnoreInvalidJson()
    {
        var sut = CreateSut();

        sut.Received("not json");

        Assert.Null(sut.Settings);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnTheSettingsTheExtensionAnswers()
    {
        var sut = CreateSut(onPost: s => s.Received(SettingsJson));

        var settings = await sut.LoadAsync();

        Assert.Equal(new VsCodeSettings(false, true, 5), settings);
        Assert.Equal(("vscode/GetSettings", ""), posted.Single());
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNullOutsideVsCode()
    {
        var sut = CreateSut(isVsCode: false);

        Assert.Null(await sut.LoadAsync());
    }

    [Fact]
    public async Task SetAutoRefreshDelaySecondsAsync_ShouldPostTheKeyAndAtLeastTheMinimum()
    {
        var sut = CreateSut();
        sut.Received(SettingsJson);

        await sut.SetAutoRefreshDelaySecondsAsync(0);

        var (type, message) = posted.Single();
        Assert.Equal("vscode/SetSetting", type);
        Assert.Equal("""{"key":"autoRefresh.delaySeconds","value":1}""", message);
        Assert.Equal(1, sut.Settings!.AutoRefreshDelaySeconds);
    }

    [Fact]
    public async Task SetFollowActiveEditorAsync_ShouldPostTheKeyAndShowTheValueAtOnce()
    {
        var sut = CreateSut();
        sut.Received(SettingsJson);

        await sut.SetFollowActiveEditorAsync(true);

        Assert.Equal(("vscode/SetSetting", """{"key":"followActiveEditor","value":true}"""), posted.Single());
        Assert.True(sut.Settings!.FollowActiveEditor);
    }
}
