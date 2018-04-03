namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// Utility to monitor the value of environment variables which may change 
    /// during the run of an application. Checks the environment variables 
    /// intermittently.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class AppServiceEnvVarMonitor
    {
        // Default list of environment variables tracked by this monitor.
        internal static IReadOnlyCollection<string> DefaultEnvVars = new string[]
        {
            "WEBSITE_SITE_NAME",
            "WEBSITE_SLOT_NAME",
            "WEBSITE_HOME_STAMPNAME",
            "WEBSITE_HOSTNAME",
            "WEBSITE_OWNER_NAME"
        };

        // Environment variables tracked by this monitor.
        internal readonly Dictionary<string, string> CheckedValues;

        // When is the next time we will allow a check to occur? (internal to allow tests to modify this to avoid waits)
        internal DateTime NextCheckTime = DateTime.MinValue;

        // how often we allow the code to re-check the environment
        private readonly TimeSpan checkInterval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes a new instance of the <see cref="AppServiceEnvVarMonitor" /> class.
        /// </summary>
        public AppServiceEnvVarMonitor() : this(AppServiceEnvVarMonitor.DefaultEnvVars)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppServiceEnvVarMonitor" /> class.
        /// This is marked internal to allow tests to override the default environment variables
        /// being monitored by this class.
        /// </summary>
        /// <param name="defaultEnvVars">List of environment variable names.</param>
        internal AppServiceEnvVarMonitor(IReadOnlyCollection<string> defaultEnvVars)
        {
            this.CheckedValues = new Dictionary<string, string>(defaultEnvVars.Count);
            foreach (string envVar in defaultEnvVars)
            {
                this.CheckedValues[envVar] = string.Empty;
            }

            this.CheckVariablesIntermittent();
        }

        /// <summary>
        /// Get the latest value assigned to an environment variable.
        /// </summary>
        /// <param name="envVarName">Name of the environment variable to acquire.</param>
        /// <param name="value">Value of the environment variable (updated within the update interval).</param>
        public void GetUpdatedEnvironmentVariable(string envVarName, ref string value)
        {
            if (!string.IsNullOrEmpty(envVarName))
            {
                this.CheckVariablesIntermittent();
                this.CheckedValues.TryGetValue(envVarName, out value);
            }
        }

        /// <summary>
        /// Simply update the stored environment variables if the last time we 
        /// checked from now is greater than the check interval.
        /// </summary>
        private void CheckVariablesIntermittent()
        {
            DateTime rightNow = DateTime.UtcNow;
            if (rightNow > this.NextCheckTime)
            {
                this.NextCheckTime = rightNow + this.checkInterval;

                List<string> keys = new List<string>(this.CheckedValues.Keys);
                foreach (var key in keys)
                {
                    this.CheckedValues[key] = Environment.GetEnvironmentVariable(key);
                }
            }
        }
    }
}
