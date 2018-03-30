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
        internal static TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
        internal static DateTime LastCheckTime = DateTime.MinValue;

        /// <summary>
        /// Environment variables tracked by this monitor.
        /// </summary>
        internal static Dictionary<string, string> CheckedValues = new Dictionary<string, string>()
        {
            { "WEBSITE_SITE_NAME", string.Empty },
            { "WEBSITE_HOME_STAMPNAME", string.Empty },
            {  "WEBSITE_HOSTNAME", string.Empty }
        };

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
            DateTime checkTime = DateTime.Now;
            if (checkTime - LastCheckTime > CheckInterval)
            {
                LastCheckTime = checkTime;
                foreach (var kvp in CheckedValues)
                {
                    CheckedValues[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                }
            }
        }
    }
}
