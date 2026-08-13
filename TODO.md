# TODO

## Multi-target .NET Framework 4.8

**The package cannot be consumed by .NET Framework at all.** `sdk/FailureFlags.csproj` declares only
`net5.0`, so restore refuses outright:

```
error NU1202: Package Gremlin.FailureFlags 1.0.0 is not compatible with net481
```

Worse, forcing it past restore with `<AssetTargetFallback>net5.0</AssetTargetFallback>` downgrades
that to a warning, the project **compiles and produces a shippable `.exe`**, and it then dies on
first touch of any SDK type with `FileNotFoundException: System.Runtime, Version=5.0.0.0`. No
binding redirect can help; that assembly does not exist on .NET Framework.

Deferred to its own PR by request. No C# changes are required — the unmodified 1.0.0 sources were
compiled against `net48` and passed the full suite on Windows Server 2025 / CLR 4.0.30319.42000.
The change is:

```xml
<TargetFrameworks>net48;net5.0</TargetFrameworks>
<LangVersion>9.0</LangVersion>

<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <PackageReference Include="System.Text.Json" Version="6.0.10" />
  <PackageReference Include="System.Net.Http.Json" Version="6.0.0" />
  <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

Notes for whoever picks this up:

- `LangVersion 9.0` is required, not cosmetic, and is required on `tests/Tests/Tests.csproj` too.
  The SDK defaults `net48` to C# 7.3 and the sources use target-typed `new()`. Omitting it on the
  test project produces `CS8370` at `GremlinFailureFlagsIntegTests.cs`.
- `System.Text.Json` 6.0.10 rather than 6.0.0: 6.0.10 is the CVE-2024-30105-patched build.
- `Microsoft.NETFramework.ReferenceAssemblies` is optional but lets the `net48` leg *build* on
  Linux/macOS CI. Executing it still needs a Windows host.
- `AssemblyBuilder.DefineDynamicAssembly` in `Behaviors/ExceptionBehavior.cs` is fine on .NET
  Framework 4.0+ and needs no `#if NETFRAMEWORK` shim. It is easy to misremember as .NET Core only.
- Consider `netstandard2.0` for wider reach instead of `net48`. Untested; verify `Reflection.Emit`
  is present before committing to it.
- `Dockerfile` uses `mcr.microsoft.com/dotnet/sdk:5.0`, which will then have to restore the `net48`
  leg on Linux. Check it.

## Smaller items

- **Remove the `[Obsolete] FailureFlags.Exception` shim** in the release after 1.1.0. It exists only
  so 1.0.x source keeps compiling, and while it exists it keeps the `CS0104` ambiguity against
  `System.Exception` that the rename was meant to fix.
- **Float `Microsoft.Extensions.Logging.Abstractions` off 5.0.0.** It is out of support. Held at
  5.0.0 in 1.1.0 to keep that release about the dependency graph rather than version churn. 6.x and
  8.x both target `netstandard2.0`, so either is compatible with the `net48` work above.
- **CI does not exist.** `FailureFlags.sln` lists a `.circleci/config.yml` in its solution items that
  is not in the repository. The sibling SDKs (`failure-flags-go`, `failure-flags-python`) use
  CircleCI with a `Makefile`; this repo has neither.
- **`Experiment.Name`, `Experiment.Guid`, and `FailureFlag.Name` are non-nullable with no
  initializer**, so every build emits three `CS8618` warnings. Left alone in 1.1.0 because fixing
  them properly means deciding whether those fields are genuinely optional on the wire.
- **`IFailureFlags.Invoke(FailureFlag, IBehavior)` declares a non-nullable behavior** while the
  implementation takes `IBehavior?` and documents null as "use the default". Align the interface.
