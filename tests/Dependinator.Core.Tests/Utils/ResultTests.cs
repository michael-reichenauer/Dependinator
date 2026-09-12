namespace Dependinator.Core.Tests.Utils;

public class ResultTests
{
    static Result<string> Get(bool ok) => ok ? "value" : new Error("failed");

    static Result Do(bool ok) => ok ? Result.Ok : new Error("failed");

    [Fact]
    public void Switch_ShouldBeExhaustiveWithBothArms()
    {
        // A switch over a result needs no discard arm; removing either arm is a build error (CS8509)
        string outcome = Do(true) switch
        {
            Success => "ok",
            Error e => e.Message,
        };
        Assert.Equal("ok", outcome);

        string value = Get(false) switch
        {
            string v => v,
            Error e => "E:" + e.Message,
        };
        Assert.Equal("E:failed", value);
    }

    [Fact]
    public void IsNotPattern_ShouldBindValueOnFallThrough()
    {
        var result = Get(true);
        if (result is not string value)
            throw new Exception(result.Error.Message);
        Assert.Equal("value", value);
    }

    [Fact]
    public void Propagation_ShouldHandOverErrorOrValue()
    {
        static Result<int> Inner(bool ok)
        {
            var result = Get(ok);
            if (result is not string value)
                return result.Error;
            if (Do(ok) is Error e)
                return e;
            return value.Length;
        }

        Assert.Equal(5, AssertOk(Inner(true)));
        Assert.Equal("failed", AssertError(Inner(false)).Message);
    }

    [Fact]
    public void ImplicitConversions_ShouldWrapValueAndError()
    {
        Result<int> value = 3;
        Result<int> error = new Error("e");
        Result outcome = new Error("o");
        Assert.Equal(3, AssertOk(value));
        Assert.Equal("e", AssertError(error).Message);
        Assert.Equal("o", AssertError(outcome).Message);
        AssertOk(Result.Ok);
    }

    [Fact]
    public void ResultOfT_ShouldConvertToResult_DroppingTheValue()
    {
        Result fromValue = Get(true);
        Result fromError = Get(false);
        AssertOk(fromValue);
        Assert.Equal("failed", AssertError(fromError).Message);
    }

    [Fact]
    public void Error_ShouldThrow_WhenResultIsAValue()
    {
        var result = Get(true);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void NullValue_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => new Result<string>((string)null!));
    }

    [Fact]
    public void Default_ShouldMatchNeitherArm()
    {
        Result<string> unset = default;
        Assert.False(unset is string);
        Assert.False(unset is Error);
        Assert.Throws<InvalidOperationException>(() => unset.Error);
        Assert.Equal("Unset", unset.ToString());
        Assert.Equal("Unset", default(Result).ToString());
    }

    [Fact]
    public void NotFoundError_ShouldMatchAsErrorAndAsItself()
    {
        Result<string> notFound = new NotFoundError("gone");
        Assert.True(notFound is Error);
        Assert.True(notFound is NotFoundError);
        Assert.False(Get(false) is NotFoundError);

        Result outcome = notFound;
        Assert.True(outcome is NotFoundError);
        Assert.IsType<NotFoundError>(AssertError(notFound));
    }

    [Fact]
    public void AllMessages_ShouldListOutermostFirst()
    {
        var chained = new Error("outer", new Error("middle", new Error("inner")));
        Assert.Equal("outer,\nmiddle,\ninner", chained.AllMessages());
        Assert.Equal("middle", chained.Inner!.Message);

        var fromException = new Error("wrapped", new InvalidOperationException("thrown", new Exception("cause")));
        Assert.Equal("wrapped,\nthrown,\ncause", fromException.AllMessages());

        // An error created from an exception already has that exception's message as its own
        var ofException = new Error(new InvalidOperationException("thrown", new Exception("cause")));
        Assert.Equal("thrown,\ncause", ofException.AllMessages());
        Assert.IsType<InvalidOperationException>(ofException.Exception);
    }

    [Fact]
    public void Origin_ShouldRecordTheCreatingLine()
    {
        var error = new Error("here");
        Assert.Contains("ResultTests.cs(", error.Origin);
        Assert.EndsWith(nameof(Origin_ShouldRecordTheCreatingLine), error.Origin);
    }

    [Fact]
    public void Catch_ShouldReturnOkOrTheException()
    {
        AssertOk(Result.Catch(() => { }));
        var error = AssertError(Result.Catch(() => throw new ArgumentException("bad arg")));
        Assert.Equal("bad arg", error.Message);
        Assert.IsType<ArgumentException>(error.Exception);
        Assert.Contains("ResultTests.cs(", error.Origin);
    }

    [Fact]
    public void CatchOfT_ShouldReturnValueOrTheException()
    {
        Assert.Equal("text", AssertOk(Result.Catch(() => "text")));
        var error = AssertError(Result.Catch<string>(() => throw new InvalidOperationException("boom")));
        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public void ToString_ShouldDescribeTheCase()
    {
        Assert.Equal("OK", Result.Ok.ToString());
        Assert.Equal("Error: failed", Do(false).ToString());
        Assert.Equal("value", Get(true).ToString());
        Assert.Equal("Error: failed", Get(false).ToString());
    }
}
