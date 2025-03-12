using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace FailureFlags
{

    /// <summary>
    /// Exception processes <code>exception</code> properties in experiment effects.
    ///
    /// Behaviors implement specific effects or symptoms of failures that an application will experience in calls to
    /// FailureFlags.invoke(...). When processing multiple experiments, delays should be applied before other failure types
    /// and those failure types that can be processed without changing flow should be applied first. If multiple experiments
    /// result in changing control flow (like exceptions, shutdowns, panics, etc.) then the behavior chain may not realize
    /// some effects.
    /// </summary>
    public class Exception : IBehavior
    {
        public void ApplyBehavior(Experiment[] experiments)
        {
            var exceptions = experiments
                .Where(experiment => experiment.Effect.ContainsKey("exception"))
                .Select(experiment => experiment.Effect["exception"])
                .ToList();

            foreach (var e in exceptions)
            {
                if (e is string exceptionName)
                {
                    System.Exception? exception = CreateException(exceptionName);
                    // If we failed to create the given exception, then fall back on a FailureFlagException
                    throw exception ?? new FailureFlagException($"Exception injected by failure flag: {exceptionName}");
                }

                if (e is Dictionary<string, object> map)
                {
                    if (map.TryGetValue("message", out var exceptionMessage))
                    {
                        throw new FailureFlagException($"Exception injected by failure flag: {exceptionMessage}");
                    }
                }
            }
        }

        private static System.Exception? CreateException(string exceptionName)
        {
            try
            {
                var assemblyName = new AssemblyName(exceptionName);
                if (assemblyName.Name == null) return null;
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
                var typeBuilder = moduleBuilder.DefineType(assemblyName.Name, TypeAttributes.Public, typeof(System.Exception));

                var exceptionType = typeBuilder.CreateType();
                if (exceptionType == null) return null;
                return (System.Exception?)Activator.CreateInstance(exceptionType);
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
