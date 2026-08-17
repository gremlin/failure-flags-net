# Architecture

## What this library is

A thin client for a Gremlin sidecar. An application calls `Invoke` at a named point in its code, the
SDK asks the sidecar whether any experiment targets that name, and if one does, it applies the
effect the experiment describes (sleep, throw, or both).

That is the whole thing. It is small on purpose.

## The fail-safe contract

The README promises Failure Flags are "safe to add to and leave in your application" and "will
always fail safe if it cannot communicate with its sidecar." Concretely:

- `Fetch` catches every exception and returns an empty array.
- `Invoke` catches everything `Fetch` might still throw and returns an empty array.
- The only exceptions that escape are the ones an active experiment asked for.

This contract has a nasty consequence for anyone testing this library, and it is worth stating
loudly: **"working perfectly" and "completely broken" produce identical output.** An empty result
means "no experiment", "sidecar not running", "sidecar returned garbage", and "the SDK failed to
load" all at once.

So tests here assert on a **positive observable** — elapsed time inside a band, a specific thrown
type, a parsed value, a recorded request. A test that asserts "nothing bad happened" asserts
nothing. The 1.0.0 release shipped with a 4.3 second fail-safe path and a kill switch that failed
open precisely because both were invisible from outside.

## Functional core, imperative shell

Configuration parsing and value coercion are pure static functions. The instance constructor and
`Fetch` are the only things that touch the world.

| Pure | Where | Why it matters |
|---|---|---|
| `GremlinFailureFlags.ParseEnabled` | `GremlinFailureFlags.cs` | The whole truth table is testable without mutating process environment, which xunit runs test classes in parallel against. |
| `GremlinFailureFlags.ResolveEndpoint` | `GremlinFailureFlags.cs` | Four-level precedence asserted directly instead of through a live HTTP call. |
| `GremlinFailureFlags.ResolveTimeout` | `GremlinFailureFlags.cs` | Same. |
| `Latency.TryToMilliseconds` | `Behaviors/Latency.cs` | Every numeric shape the wire can produce, covered by a table. |

The shell reads `Environment.GetEnvironmentVariable` exactly once per instance, in the constructor,
and passes the strings into those functions. Nothing downstream knows an environment exists.

## Configuration precedence

Endpoint, most specific first:

```
constructor argument
  -> FAILURE_FLAGS_ENDPOINT              (a full URL)
    -> GREMLIN_SIDECAR_HOST / _PORT      (either may be set alone)
      -> http://localhost:5032/experiment
```

Timeout: constructor argument, then `FAILURE_FLAGS_TIMEOUT_MS`, then 50ms.

Bad configuration never throws. A port that is not a number in 1..65535, or a timeout that is not a
positive integer, falls back to the default. A fault injection library that takes the application
down over a malformed environment variable has failed at its one job.

## HTTP

One `static readonly HttpClient` for the process, with its own timeout set to infinite.

Both halves of that matter. Constructing an `HttpClient` per call constructs a connection pool per
call and leaves the socket in `TIME_WAIT` on dispose, which exhausts ephemeral ports on any path
that runs per request. And because the client is shared while the deadline is per instance, the
timeout cannot live on the client — each `Fetch` creates a `CancellationTokenSource` and passes its
token to `SendAsync` and `ReadFromJsonAsync`.

`Fetch` blocks on `.Result`. This is sync-over-async and it does park a thread-pool thread for the
duration of the call, which is why the deadline is 50ms. It is **not** a deadlock risk under a
single-threaded `SynchronizationContext`: `HttpClient` and `System.Net.Http.Json` use
`ConfigureAwait(false)` throughout, so nothing is ever posted back to the captured context. This was
measured on .NET Framework 4.8.1 under a context that captures continuations and never runs them;
275ms, no deadlock, zero continuations posted. Do not restructure it on deadlock grounds.

## Behaviors

`IBehavior` has one method, `ApplyBehavior(Experiment[])`. Three implementations ship:

- `Latency` sleeps.
- `ExceptionBehavior` throws, synthesizing a named exception type via `Reflection.Emit` when the
  effect names one, and falling back to `FailureFlagException`.
- `DelayedException` composes the two, latency first, and is the default.

Ordering is the reason `DelayedException` exists: effects that change control flow have to run last,
because once one throws, nothing after it in the chain runs.

Behaviors take an optional `ILogger`. They need it because the alternative to logging an effect they
cannot apply is dropping it silently, which leaves an operator staring at a green experiment in the
console with nothing anywhere to explain why nothing happened. Malformed effects log and skip rather
than throw; a broken experiment definition should not become an outage.

Every behavior has an explicit parameterless constructor alongside the logger-taking one, rather
than a single constructor with an optional parameter. Optional parameters do not produce a
zero-argument constructor in metadata, and the test suite mocks `Latency` through Castle
DynamicProxy, which needs one.

## Randomness

`Rng` holds one `[ThreadStatic] Random`, explicitly seeded. `Random` is not thread safe, and
allocating one per roll is waste on a per-request path. It is deliberately not `Random.Shared`,
which is .NET 6+; see below.

Each experiment gets its own roll. Sharing one roll across every experiment in a batch makes
experiments that are supposed to be statistically independent perfectly correlated: at a roll of
0.3, every experiment with a rate above 0.3 fires together and every one below never fires.

## Target framework

`net48;net8.0;net9.0;net10.0`. `net5.0`, the only target of the published 1.0.0, is gone; that is the
breaking change behind the 2.0.0 major.

`net48` is the floor, and it is a low one, so **new code must not use APIs outside .NET Framework
4.8's surface** unless it is behind a `$(TargetFramework)` condition. Live consequences: `Random.Shared`
is off limits (.NET 6+), and `System.Text.Json` / `System.Net.Http.Json` arrive as explicit
`PackageReference`s on the `net48` leg where net8.0+ gets them from the shared framework.
`net48` also defaults to C# 7.3, so `LangVersion` is pinned to 9.0 for that leg in both the SDK and
the test project; the sources use target-typed `new()`.

`Microsoft.NETFramework.ReferenceAssemblies` lets the `net48` leg *build* on Linux and macOS.
Executing it still needs a Windows host, so `dotnet test` on CI covers net8.0+ only.

## Versioning

The `VERSION` file is the single source of truth; `sdk/FailureFlags.csproj` reads it into
`<Version>` at build time. The SDK reports itself to Gremlin in a `failure-flags-sdk-version` label,
and it reads that string from `AssemblyInformationalVersionAttribute` rather than
`Assembly.GetName().Version`, because the latter is always four parts and cannot represent the
three-part version in the file. `IncludeSourceRevisionInInformationalVersion` is off so the label
does not become `2.0.0+<sha>`.

## Cross-SDK consistency

Failure Flags ships in Go, Python, Node, Java, and .NET, and they do not all agree. Where this SDK
deviates, it is deliberate:

- **`FAILURE_FLAGS_ENABLED` parsing** matches Go and the public documentation. Python checks only
  for the key's presence.
- **Per-experiment probability rolls and returning the applied set** deviate from Python, which uses
  one shared roll and returns everything fetched.
- **The endpoint override** exists only here; every other SDK hardcodes `localhost:5032`.
- **The 50ms timeout** is looser than Go's 2ms and Python's 1ms, because `HttpClient` on a cold
  connection is not `urlopen`.
