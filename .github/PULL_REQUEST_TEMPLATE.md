## What

<!-- What does this change and why? Link the issue if there is one: Fixes #123 -->

## Checklist

- [ ] Targets `dev` (`main` is release-only)
- [ ] `dotnet build Dependinator.sln` and `dotnet test Dependinator.sln` pass
- [ ] `./scripts/e2e` passes if the UI changed
- [ ] Code is formatted (`dotnet csharpier --check .`)
- [ ] User-facing change? Add a one-line note for the changelog in the description above
