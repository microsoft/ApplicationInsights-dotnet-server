namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Utility to monitor the value of environment variables which may change 
    /// during the run of an application. Checks the environment variables 
    /// intermittently.
    /// </summary>
    internal static class AppServiceEnvVarMonitor
    {
        // Environment variables tracked by this monitor. (internal to allow tests to modify them)
        internal static Dictionary<string, string> CheckedValues = new Dictionary<string, string>()
        {
            { "WEBSITE_SITE_NAME", string.Empty },
            { "WEBSITE_HOME_STAMPNAME", string.Empty },
            { "WEBSITE_HOSTNAME", string.Empty }
        };

        // When is the next time we will allow a check to occur? (internal to allow tests to modify this to avoid waits)
        internal static DateTime NextCheckTime = DateTime.MinValue;

        // how often we allow the code to re-check the environment
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Get the latest value assigned to an environment variable.
        /// </summary>
        /// <param name="envVarName">Name of the environment variable to acquire.</param>
        /// <param name="value">Value of the environment variable (updated within the update interval).</param>
        public static void GetUpdatedEnvironmentVariable(string envVarName, ref string value)
        {
            if (!string.IsNullOrEmpty(envVarName))
            {
                CheckVariablesIntermittent();
                CheckedValues.TryGetValue(envVarName, out value);
            }
        }

        /// <summary>
        /// Simply update the stored environment variables if the last time we 
        /// checked from now is greater than the check interval.
        /// </summary>
        private static void CheckVariablesIntermittent()
        {
            DateTime rightNow = DateTime.UtcNow;
            if (rightNow > NextCheckTime)
            {
                NextCheckTime = rightNow + CheckInterval;

                List<string> keys = new List<string>(CheckedValues.Keys);
                foreach (var key in keys)
                {
                    CheckedValues[key] = Environment.GetEnvironmentVariable(key);
                }
            }
        }
    }
}
