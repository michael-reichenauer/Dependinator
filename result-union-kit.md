# Result union kit

Everything needed to adopt gmd's `Result` / `Result<T>` type in another C# project. It was worked
out in gmd (github.com/michael-reichenauer/gmd, branch `worktree-mr+refactor`, September 2026) on
the .NET 11 RC1 compiler, where it replaced a `Try(out value, out e, …)` result type across about
480 call sites with all 1044 tests green. Section 8 is a prompt to start a plan with.

## 1. What it is

A result is a C# 15 **union**: `Result<T>` is either a value `T` or an `Error`, and `Result` is
either `Success` or an `Error`. Both are structs the C# 15 compiler recognizes as unions (the
`[Union]` attribute, one public constructor per case type, an `object? Value`), which gives:

- A `switch` over a result is exhaustive with its two arms; a missing arm is a build error.
- A pattern applies to the contained value, not to the struct: `result is Error e`,
  `result is not Status status`.
- Implicit conversions from the value and from an `Error`, so a method just returns either.

There is no `Try` helper, no `Match` method, no conversion to `bool`. Exceptions are for bugs.

```csharp
// Propagate, with a value: the pattern binds it, the error is returned when there is none
var result = await git.GetStatusAsync(wd);
if (result is not Status status) return result.Error;

// Propagate, no value
if (await git.SetValueAsync(key, json, wd) is Error e) return e;

// Branch on the outcome: exhaustive, no discard arm
return await server.PullAsync(name, wd) switch
{
    Success => Result.Ok,
    Error e => new Error("Failed to pull", e),
};

// A fallback instead of an error
var tags = await git.GetTagsAsync(wd) is IReadOnlyList<Tag> t ? t : [];

// Create and wrap errors; the constructor records the caller's file and line as Origin
return new Error($"Folder missing: {path}");
return new Error("Failed to merge", inner: e);

// A throwing API becomes a result at the boundary
if (Result.Catch(() => File.Move(src, dst)) is Error e) return e;

// Returning: implicit conversions mean you just return the value or the error
return commits;      // Result<IReadOnlyList<Commit>>
return Result.Ok;    // Result

// Tests (MSTest helpers below)
var commits = AssertOk(await log.GetLogAsync(100, "/wd"));
var e = AssertError(await log.GetLogAsync(100, "/wd"));
```

Design decisions, so they are not re-litigated:

- `result.Error` is the one accessor that trusts the caller: it throws on a value, so it is used
  only right after a pattern ruled the value out, as above. There is no typed value accessor; the
  pattern binds the value.
- `Value` is the compiler's window into the union, not an accessor: every pattern is lowered to a
  pattern on it, so it returns the `Error` too and never throws. Match, do not read it. Generic
  code is the exception (below).
- The spec's optional non-boxing members (`HasValue`, `TryGetValue`) are left out on purpose: they
  only pay off for a union that keeps value types unboxed, and these store one boxed object.
- An `Error` wraps at most one thing, another `Error` or an exception; `AllMessages()` is its
  message followed by the wrapped one's messages, outermost first (what a dialog shows).
- One `Error` root; subclass it when a caller must tell kinds apart (gmd has `CmdError` with the
  exit code and outputs of a failed command). An *expected* alternative outcome is a value, modeled
  as its own case, not an error. No `Result<T1, T2>` arity ladder: name a domain union instead.
- `Result.Catch` catches every exception, the "fatal" ones included, because file and process
  APIs report bad input by throwing `ArgumentException` and `InvalidOperationException`.
- `where T : notnull`: a null value is a bug in the function, and throws.

## 2. Traps met on the compiler (RC1), all still true

- **Name the exact type in a pattern.** A pattern naming a type that is not a case type compiles
  and never matches. In gmd, `Commit`, `Status`, `ConflictFile` exist in two namespaces, so a
  pattern on the wrong twin was silent. Spell the type as the file already does.
- **Generic code:** `result is T value` is refused (CS8780: with a type parameter the compiler
  cannot tell the struct from its contents). Match `result.Value is T value` there.
- **Tuples** cannot be bound by a union pattern that declares a variable (`is not (a, b)` is an
  error). A result that carried a tuple gets a small record instead.
- **Pattern variables leak to the enclosing block.** Two `if (… is Error e)` guards in one method
  body clash; name them (`pushError`, `deleteError`). A guard inside a nested block clashes with an
  outer `e` too (CS0136).
- `not` applies to the union itself and every other pattern to its contents, which is what makes
  `if (result is not Status status) return …;` bind `status` on the fall-through.
- A type named `Error` cannot be referred to inside a type that also has a *method* named
  `Error` (a property is fine, the "Color Color" rule). Hence `new Error(...)`, no factory on
  `Result`.
- `Result<T>` to `Result<U>` cannot be expressed by any operator (no generic operators, no
  conversion from an interface or base class, no chaining of user-defined conversions), so a
  guard hands over `result.Error`. `Result<T>` to `Result` exists and drops the value.
- `default(Result<T>)` holds nothing and matches neither arm; a `switch` on it throws.

## 3. Environment

The union *semantics* live in the compiler, so the .NET 11 SDK is needed while the target
framework can stay where it is (net10.0 in gmd; the polyfill below covers net8.0 to net10.0).

- `global.json` at the repo root (the GA version in November 2026 is `11.0.100`):

  ```json
  { "sdk": { "version": "11.0.100-rc.1.26425.128", "rollForward": "latestFeature", "allowPrerelease": true } }
  ```

- `Directory.Build.props` at the repo root (`preview` until GA, then `15`; drop it entirely when the
  target framework becomes net11.0):

  ```xml
  <Project>
    <PropertyGroup>
      <LangVersion>preview</LangVersion>
      <!-- A switch over a result that does not handle both of its cases is a bug, not a warning -->
      <WarningsAsErrors>$(WarningsAsErrors);CS8509</WarningsAsErrors>
    </PropertyGroup>
  </Project>
  ```

- Install the SDK beside the existing one (Linux devcontainer; `--quality preview` until GA):

  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh | sudo bash -s -- --channel 11.0 --quality preview --install-dir /usr/share/dotnet
  ```

- CI with `actions/setup-dotnet`: `global-json-file: global.json` **and** `dotnet-version:
  '10.0.x'` (or whatever the target runtime is), because `dotnet test` of a net10.0 project needs
  that runtime, which the 11 SDK does not carry.
- **CSharpier** (1.3.0, the latest in September 2026) parses no C# 15 syntax. The kit needs none:
  the `union` keyword is not used, patterns are old syntax with new semantics. Do not write
  `union X(...)` declarations until a CSharpier release supports C# 15.
- The VS Code C# extension bundles its own Roslyn and may flag union patterns until it catches
  up; `dotnet build` is the truth.

## 4. The three files

Change the namespaces to the project's; nothing else depends on gmd. In the test project add
`global using static <TestNamespace>.ResultAssert;`; the helper uses MSTest's
`AssertFailedException`, swap in the equivalent for xUnit or NUnit.

### `Result.cs`

```csharp
using System.Runtime.CompilerServices;

namespace gmd.Utils;

// Every fallible operation returns a Result or a Result<T>: a union of its value (or Success) and an Error,
// matched on the case type. Exceptions are for bugs, not for flow control.
//
//   var result = await git.GetStatusAsync(wd);
//   if (result is not Status status) return result.Error;
//   ... status is a Status from here on
//
//   if (await git.SetValueAsync(key, json, wd) is Error e) return e;
//
//   return await server.PullAsync(name, wd) switch
//   {
//       Success => Result.Ok,
//       Error e => new Error("Failed to pull", e),
//   };
//
//   var tags = await git.GetTagsAsync(wd) is IReadOnlyList<Tag> t ? t : [];
//
// Result and Result<T> are custom unions in the C# 15 sense: a struct with the [Union] attribute, one public
// constructor per case type and an object Value. So a switch over one is exhaustive without a
// discard arm (a missing arm is a build error), and a pattern applies to the contained value rather
// than to the struct. The optional non-boxing members of that pattern (HasValue, TryGetValue) are
// deliberately absent: they are for unions that keep value types unboxed in fields of their own,
// and these store their contents as one object, so the compiler matches on Value, as it does for
// its own union declarations. The attribute is polyfilled while the target framework is net10.0,
// see UnionPolyfill.cs.

// The failure case of Result and Result<T>: a message, where it was created, and optionally the
// one thing it wraps, which is either another Error or an exception.
public class Error
{
    // What this error wraps: an Error, an Exception, or nothing. One field rather than one per
    // kind, so an error can never wrap both, which would leave no order for their messages.
    readonly object? cause;

    public Error(
        string message = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, (object?)null, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        string message,
        Error inner,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, (object)inner, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        string message,
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, (object)exception, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(exception.Message, (object)exception, memberName, sourceFilePath, sourceLineNumber) { }

    Error(string message, object? cause, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        Message = message;
        this.cause = cause;
        Origin = $"{sourceFilePath}({sourceLineNumber}) {memberName}";
    }

    public string Message { get; }

    // The error this one wraps, when that is what it wraps
    public Error? Inner => cause as Error;

    // The exception this one wraps, when that is what it wraps
    public Exception? Exception => cause as Exception;

    // The file, line and member that created the error, since nothing is thrown and so there is no
    // stack trace to tell
    public string Origin { get; }

    // This message and then those of what it wraps, outermost first, which is what a dialog shows
    public string AllMessages() => string.Join(",\n", Messages());

    IEnumerable<string> Messages()
    {
        yield return Message;

        if (cause is Error inner)
        {
            foreach (var message in inner.Messages())
                yield return message;
        }

        // An error created from an exception already has that exception's message as its own
        var exception = cause as Exception;
        if (exception != null && exception.Message == Message)
            exception = exception.InnerException;

        for (; exception != null; exception = exception.InnerException)
            yield return exception.Message;
    }

    public override string ToString() => $"Error: {Message}";
}

// The success case of Result
public sealed class Success
{
    public static readonly Success Instance = new();

    Success() { }

    public override string ToString() => "OK";
}

// The result of an operation that either succeeds or fails: Success or Error
[Union]
public readonly struct Result : IUnion
{
    readonly object? value;

    public Result(Success success) => value = success;

    public Result(Error error) => value = error;

    public static readonly Result Ok = new(Success.Instance);

    // The contained case, for the compiler: every pattern on a result is lowered to a pattern on
    // Value, so it returns the Error as well, and null for default(Result). Match; do not read it.
    public object? Value => value;

    public static implicit operator Result(Error error) => new(error);

    // Runs an action that reports failure by throwing, e.g. a file API, and returns the exception
    // as an error. Every exception is caught, the fatal ones included, since bad input to such an
    // API surfaces as an ArgumentException or an InvalidOperationException.
    //
    //   if (Result.Catch(() => File.Move(source, target)) is Error e) return e;
    public static Result Catch(
        Action action,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        try
        {
            action();
            return Ok;
        }
        catch (Exception e)
        {
            return new Error(e, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    // Runs a function that reports failure by throwing and returns its value, or the exception as
    // an error.
    //
    //   var text = Result.Catch(() => File.ReadAllText(path));
    //   if (text is not string content) return text.Error;
    public static Result<T> Catch<T>(
        Func<T> func,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        where T : notnull
    {
        try
        {
            return func();
        }
        catch (Exception e)
        {
            return new Error(e, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    public override string ToString() =>
        value switch
        {
            Error e => e.ToString(),
            Success => "OK",
            _ => "Unset",
        };
}

// The result of an operation that either produces a value or fails: T or Error
[Union]
public readonly struct Result<T> : IUnion
    where T : notnull
{
    readonly object? value;

    // A null value is not an error, it is a bug in the function returning it
    public Result(T value) =>
        this.value = value ?? throw new InvalidOperationException("A result value cannot be null");

    public Result(Error error) => value = error;

    // The contained case, for the compiler: every pattern on a result is lowered to a pattern on
    // Value, so it returns the Error as well, and null for default(Result<T>). Match rather than
    // read it. The one exception is generic code, which cannot bind a type parameter in a union
    // pattern and matches 'result.Value is T value' instead.
    public object? Value => value;

    // The error of a result already known to be one, i.e. right after a pattern ruled out the
    // value; reading it on a value is a bug in the caller:
    //
    //   if (result is not Status status) return result.Error;
    public Error Error => value as Error ?? throw new InvalidOperationException("Result is not an error");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);

    // Dropping the value keeps the outcome
    public static implicit operator Result(Result<T> result) => result.value is Error e ? new Result(e) : Result.Ok;

    public override string ToString() =>
        value switch
        {
            Error e => e.ToString(),
            null => "Unset",
            _ => value.ToString() ?? "",
        };
}
```

### `UnionPolyfill.cs`

```csharp
// The attribute and interface the C# 15 compiler recognizes a union type by. They ship in the .NET 11
// base class library; while the target framework is net10.0 they are declared here instead, which is
// the documented way (the compiler matches them by name). Delete this file when the target framework
// becomes net11.0.
#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
internal sealed class UnionAttribute : Attribute;

internal interface IUnion
{
    object? Value { get; }
}
#endif
```

### `ResultAssert.cs` (test project)

```csharp
namespace gmdTest.Fixtures;

// Assertions on Result and Result<T> that hand back the case they assert, so a test reads
//
//     var commits = AssertOk(await log.GetLogAsync(100, "/wd"));
//     var e = AssertError(await log.GetLogAsync(100, "/wd"));
//
// Available everywhere through the static global using in Usings.cs.
public static class ResultAssert
{
    // Matched on Value rather than 'result is T value': a union pattern whose type is a type
    // parameter cannot declare a variable, since the compiler cannot tell the struct from its contents
    public static T AssertOk<T>(Result<T> result, string message = "")
        where T : notnull
    {
        if (result.Value is T value)
            return value;
        throw new AssertFailedException($"Expected a value but got {result.Error.AllMessages()}{Suffix(message)}");
    }

    public static void AssertOk(Result result, string message = "")
    {
        if (result is Error e)
            throw new AssertFailedException($"Expected success but got {e.AllMessages()}{Suffix(message)}");
    }

    public static Error AssertError<T>(Result<T> result, string message = "")
        where T : notnull
    {
        if (result is Error e)
            return e;
        throw new AssertFailedException($"Expected an error but got the value {result}{Suffix(message)}");
    }

    public static Error AssertError(Result result, string message = "")
    {
        if (result is Error e)
            return e;
        throw new AssertFailedException($"Expected an error but got success{Suffix(message)}");
    }

    static string Suffix(string message) => message == "" ? "" : $": {message}";
}
```

Things that had to change around it in gmd, likely to recur: a fire-and-forget helper taking
`Task` silently dropped an error result until it got a `Task<Result>` overload that logs it; a
type that derived from `R<string>` to carry extra data (the process result) became a record plus
an `Error` subclass, since a struct has no subclasses; two methods returned tuples and got records.

## 5. Migration playbook, if the project has the old `R` / `R<T>` with `Try`

Done in gmd as one branch of small commits, each building and passing the suite:

0. A half-hour spike outside the repo on the new SDK, proving: exhaustive `switch` without a
   discard, `is not T value` binding on the fall-through, a subtype of a case type matching, and
   the formatter passing. Keep the spike output in the first commit message.
1. Environment only (section 3); the unchanged tree builds and tests green.
2. The new types plus a **temporary `Try` shim** with the old six signatures over them, so the
   tree compiles with only the sites the compiler forces changed (`R.Error(` to `new Error(`,
   `IsResultError`, implicit-bool uses, anything deriving from the old class). Rewrite the result
   type's own tests now.
3. Convert one layer per commit, bottom up. Two shapes are mechanical and worth a script: the
   outcome guard `if (!Try(out var e, X)) stmt;` becomes `if (X is Error e) stmt;`, and a value
   guard whose type is known becomes `var xResult = X; if (xResult is not T x) return
   xResult.Error;`. Everything else needs the value's type by hand. Name the union local
   `result` when a method has one, `<value>Result` when it has several.
4. Tests: `Assert.IsTrue(Try(out var x, out var e, X), $"{e}")` becomes `var x = AssertOk(X);`,
   `Assert.IsFalse(Try(...))` becomes `AssertError(X)` (or `var e = AssertError(X)` when the
   error is inspected).
5. Delete the shim and its `global using static`, update the guidance files.

Three mistakes made in gmd, so the plan can guard against them: a regex rename that touched an
`R` inside a raw string literal (a git status line in a test fixture); reading only the last lines
of a test run and missing a failure; a value-only `Try(out var value, X)` misread as an error guard.
Review every changed line that sits inside a string literal, and read the full test summary.

## 6. Verification

- `dotnet build` clean with zero warnings; the formatter check clean; the whole suite green.
- A grep that finds nothing: `Try(` (other than `TryGetValue`, `TryParse`), the old factory, the
  old error type name.
- One test that a `switch` over a result with both arms builds, and that removing an arm fails
  the build (CS8509 is an error via `Directory.Build.props`).
- A smoke run of the built program on a failing operation, to see the user-facing error text is
  unchanged (`AllMessages()` reproduces the old "outer message, inner messages" format).

## 7. At .NET 11 GA (November 2026)

`global.json` to the GA version without `allowPrerelease`; `LangVersion` to `15`; drop
`--quality preview` from the install line. If the target framework moves to net11.0 (STS; net10.0
is LTS to November 2028): delete `UnionPolyfill.cs` and the explicit `LangVersion`, and drop the
extra runtime line in CI. If the base class library ships a standard `Result` type of its own, the
build reports the clash as CS0104; resolve it then.

## 8. A prompt to start with

> This project should adopt the union-based `Result` / `Result<T>` type described in
> `result-union-kit.md` (read it first, all of it). Plan the change: (1) find how errors are
> reported here today and every site that would change, with counts by shape; (2) the environment
> step (SDK, `global.json`, `Directory.Build.props`, CI, formatter), verified green before any code
> changes; (3) the new types and test helper, with the type's own tests; (4) the migration in
> layer-sized commits, bottom up, behind a temporary shim if there is an old `Try`; (5) removal of
> the shim and the documentation. Name the traps from the kit that apply here and how each commit
> is verified. Do not start the `union` keyword; the formatter cannot parse it yet.
