using FailureFlags;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;

namespace BasicExample
{
    public class Program
    {
        public static void Main()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            var gremlinFailureFlags = new GremlinFailureFlags(null, loggerFactory.CreateLogger<GremlinFailureFlags>());
            while (true)
            {
                {
                    var latencyFailureFlag = new FailureFlag
                    {
                        Name = "http-ingress",
                        Labels = new Dictionary<string, string> {
                    { "method", "GET" },
                    { "path", "/api/v1/health" },
                    },
                        Debug = true
                    };
                    var stopwatch = Stopwatch.StartNew();
                    gremlinFailureFlags.Invoke(latencyFailureFlag, new Latency());
                    stopwatch.Stop();
                    System.Console.WriteLine(
                        $"Invoke took {stopwatch.ElapsedMilliseconds} ms."
                    );
                }

                {
                    var exceptionFailureFlag = new FailureFlag
                    {
                        Name = "test-exception",
                        Labels = new Dictionary<string, string> { },
                        Debug = true
                    };
                    gremlinFailureFlags.Invoke(exceptionFailureFlag, new Exception());
                }

                {
                    var delayedExceptionFailureFlag = new FailureFlag
                    {
                        Name = "test-delayed-exception",
                        Labels = new Dictionary<string, string> { },
                        Debug = true
                    };
                    gremlinFailureFlags.Invoke(delayedExceptionFailureFlag, new DelayedException());
                }

                System.Threading.Thread.Sleep(5000);
            }

        }
    }
}
