#if RUN_IN_CONTAINER
using System.Collections;
using System.Text.RegularExpressions;

namespace ContainerDebugging.ServiceDefaults
{
    public partial class ContainerHelpers
    {
        [GeneratedRegex("(/)localhost([:/])", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex LocalhostRegex();

        public static void WaitForSetup()
        {
            Regex regex = LocalhostRegex();
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                string value = entry.Value as string ?? string.Empty;

                if (value.Contains("localhost"))
                {
                    string newValue = regex.Replace(value, "$1host.docker.internal$2");
                    Environment.SetEnvironmentVariable((string)entry.Key, newValue);
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