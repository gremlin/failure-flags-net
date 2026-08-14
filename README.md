# Gremlin Failure Flags for .NET

Failure Flags is a .NET SDK for building application-level chaos experiments and reliability tests using the Gremlin Fault Injection platform. This library works in concert with Gremlin-Lambda, a Lambda Extension; or Gremlin-Sidecar, a container sidecar agent. This architecture minimizes the impact to your application code, simplifies configuration, and makes adoption painless.

Just like feature flags, Failure Flags are safe to add to and leave in your application. Failure Flags will always fail safe if it cannot communicate with its sidecar or its sidecar is misconfigured.

Take three steps to run an application-level experiment with Failure Flags:

1. Instrument your code with this SDK
2. Configure and deploy your code alongside one of the Failure Flag sidecars
3. Run an Experiment with the console, API, or command line

The SDK is available on nuget.org as [Gremlin.FailureFlags](https://www.nuget.org/packages/Gremlin.FailureFlags/).

## Getting Started with the Demo App

### To deploy the demo application to a minikube cluster:

#### Build the solution
```
$ dotnet build
```

#### Build the docker image with the BasicExample demo app
```
$ docker build -t basicexample:dev .
```

#### Start a minikube cluster
```
$ minikube -p <profile> start
```

#### Load the docker image into minikube
```
$ minikube -p <profile> image load basicexample:dev
```

#### Create a pod spec
Update  [examples/BasicExample/k8s-deployment.yaml](examples/BasicExample/k8s-deployment.yaml) with your credentials.
For more information, see [Gremlin Failure Flags Kubernetes Documentation](https://www.gremlin.com/docs/failure-flags-kubernetes).

#### Add the deployment to the cluster
```
$ kubectl --context <profile> apply -f examples/BasicExample/k8s-deployment.yaml
```

### Inject latency by running a Failure Flags Experiment

Create a new [experiment](https://app.gremlin.com/failure-flags/new) with the following settings:
- Experiment Name: `my-failure-flag-experiment` (or a descriptive name for your experiment)
- Failure Flag Selector: `http-ingress`
- Service Selector: `dot-net-application`
- Effects: latency with attributes 'ms' = `10000` (10 seconds or the latency you'd like to inject) and 'jitter' = `0` (or a different value to introduce variability in latency)
- Impact Probability: `1%` (or a different percentage to control the likelihood of the fault occuring)
- Experiment Duration: `1 min` (or a duration that fits your testing needs)

Trigger the experiment by selecting `Save and Run`.

Verify the impact by checking the logs of the the demo app container:
```
kubectl logs deployment/sidecar-demo -c basicexample -f
```

The logs will indicate that invoke took ~ 10s.

```
info: FailureFlags.GremlinFailureFlags[0]
      fetching experiments for: name: http-ingress, labels: [method, GET], [path, /api/v1/health], [failure-flags-sdk-version, failure-flags-net-v1.1.0]
info: FailureFlags.GremlinFailureFlags[0]
      1 fetched experiments
Invoke took 10020 ms.
```

### Inject an exception by running a Failure Flags Experiment

Create a new [experiment](https://app.gremlin.com/failure-flags/new) with the following settings:
- Experiment Name: `my-failure-flag-experiment` (or a descriptive name for your experiment)
- Failure Flag Selector: `test-exception`
- Service Selector: `dot-net-application`
- Effects: `exception` with attribute 'message' = `TextException` (or a custom exception message)
- Impact Probability: `1%` (or a different percentage to control the likelihood of the fault occuring)
- Experiment Duration: `1 min` (or a duration that fits your testing needs)

Trigger the experiment by selecting `Save and Run`.

The logs will indicate that an exception was thrown.
```
Unhandled exception. FailureFlags.FailureFlagException: Exception injected by failure flag: TestException
   at FailureFlags.ExceptionBehavior.ApplyBehavior(Experiment[] experiments) in /sdk/src/FailureFlags/Behaviors/ExceptionBehavior.cs:line 51
   at FailureFlags.GremlinFailureFlags.Invoke(FailureFlag flag, IBehavior behavior) in /sdk/src/FailureFlags/GremlinFailureFlags.cs:line 283
   at BasicExample.Program.Main() in /examples/BasicExample/Program.cs:line 44
```


## Configuration

Everything is configured by environment variable. The endpoint and timeout can also be passed to the
`GremlinFailureFlags` constructor, which takes precedence over the environment.

| Variable | Default | Meaning |
|---|---|---|
| `FAILURE_FLAGS_ENABLED` | unset (disabled) | Enables the SDK. Must be `true`, `yes`, or `1`, case insensitively. Any other value, including `false` and the empty string, leaves the SDK disabled. |
| `FAILURE_FLAGS_ENDPOINT` | `http://localhost:5032/experiment` | Full sidecar URL. Takes precedence over `GREMLIN_SIDECAR_HOST` and `GREMLIN_SIDECAR_PORT`. |
| `GREMLIN_SIDECAR_HOST` | `localhost` | Sidecar host. Either this or the port may be set alone. |
| `GREMLIN_SIDECAR_PORT` | `5032` | Sidecar port. A value that is not a number in 1..65535 falls back to the default. |
| `FAILURE_FLAGS_TIMEOUT_MS` | `50` | How long the SDK will wait for the sidecar before giving up and returning no experiments. |

The timeout matters more than it looks. The sidecar is a co-process on loopback, so anything slower
than a few tens of milliseconds is not going to answer, and a Failure Flag must never be the reason
one of your requests is slow. If your sidecar runs somewhere with a real network in between, raise
it deliberately rather than leaving it to chance.

## Instrumenting Your Code

You'll need to enable the SDK by setting the `FAILURE_FLAGS_ENABLED` environent variable when you run the application where you add the SDK.

You can get started by adding the [Gremlin.FailureFlags package](https://www.nuget.org/packages/Gremlin.FailureFlags) to your project dependencies. Run the following command in your project directory:
```
dotnet add package Gremlin.FailureFlags
```
‍
Then bring in the library and instrument the part of your application where you want to inject faults.
```
using System.Collections.Generic;
using FailureFlags;

...
var gremlin = new GremlinFailureFlags();

gremlin.Invoke(new FailureFlag()
{
    Name = "http-ingress",
    Labels = new Dictionary<string, string>()
    {
        { "method", "GET" },
        { "path", "/api/v1/health" }
    }
});
...
```

## Building and Testing the SDK


### To build the SDK
```
$ dotnet build --configuration [Debug|Release]
```

### To run the tests
```
$ dotnet test
```

## Release notes

### 1.1.0

Three changes alter observable behavior. Read these before upgrading.

**`FAILURE_FLAGS_ENABLED` is now parsed, not just tested for presence.** Previously any value at all
enabled the SDK, so `FAILURE_FLAGS_ENABLED=false` *enabled* fault injection. It now requires `true`,
`yes`, or `1`. If you deploy through anything that renders booleans rather than omitting keys (Helm,
ECS task definitions, Kubernetes ConfigMaps, Terraform) and you were setting it to `false`, injection
was on and this release turns it off.

**`Invoke` returns the experiments it applied, not everything it fetched.** Callers inspecting the
return value to find out what actually fired now get that, rather than the unfiltered set. Relatedly,
each experiment now gets its own probability roll; a single roll shared across every experiment made
supposedly independent experiments perfectly correlated.

**`FailureFlags.Exception` is now `FailureFlags.ExceptionBehavior`.** The old name is an ambiguous
reference against `System.Exception` for any consumer with `using FailureFlags;`, which breaks every
bare `catch (Exception e)` with CS0104. `FailureFlags.Exception` survives as an `[Obsolete]` subclass
so existing source keeps compiling, but it keeps the ambiguity alive and will be removed in the next
release.

Also in this release: a 50ms default timeout on the sidecar fetch (there was none, so `HttpClient`'s
100 second default governed); a configurable endpoint; `Latency` treats `jitter` as optional and
accepts non-`Int32` numbers instead of silently injecting nothing; effects the SDK cannot apply are
logged rather than dropped in silence; `Fetch` no longer rewrites the `FailureFlag` you hand it; and
one shared `HttpClient` instead of one per call.

