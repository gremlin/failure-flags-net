# TODO

## Smaller items

- **Remove the `[Obsolete] FailureFlags.Exception` shim.** It exists only so 1.0.x source keeps
  compiling, and while it exists it keeps the `CS0104` ambiguity against `System.Exception` that the
  rename was meant to fix. 2.0.0 already drops `net5.0`, so this is the release to do it in; it was
  left alone only to keep the multi-targeting change separable.
- **Consider `netstandard2.0` in place of `net48`** for wider reach. Untested; verify
  `Reflection.Emit` (`AssemblyBuilder.DefineDynamicAssembly` in `Behaviors/ExceptionBehavior.cs`) is
  present before committing to it.
- **CI does not exist.** `FailureFlags.sln` lists a `.circleci/config.yml` in its solution items that
  is not in the repository. The sibling SDKs (`failure-flags-go`, `failure-flags-python`) use
  CircleCI with a `Makefile`; this repo has neither. Whatever gets built needs a Windows job if the
  `net48` leg is going to be *executed* rather than merely compiled.
- **`IFailureFlags.Invoke(FailureFlag, IBehavior)` declares a non-nullable behavior** while the
  implementation takes `IBehavior?` and documents null as "use the default". Align the interface.
