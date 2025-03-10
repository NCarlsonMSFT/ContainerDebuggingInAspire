#if RUN_IN_CONTAINER
using System.Diagnostics;

namespace ContainerDebugging.ServiceDefaults
{
    public class ContainerHelpers
    {
        public static void WaitForSetup()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("WAIT_FOR_DEBUGGING"), "true"))
            {
                while(!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
            }

            if (string.Equals(Environment.GetEnvironmentVariable("WAIT_FOR_CERT"), "true"))
            {
                while (!Path.Exists("/tmp/certready"))
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
#endif