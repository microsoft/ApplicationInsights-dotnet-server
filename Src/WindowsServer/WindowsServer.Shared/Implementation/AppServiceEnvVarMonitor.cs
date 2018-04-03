namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading;

    internal delegate void MonitoredEnvironmentVariableUpdated();

    /// <summary>
    /// Utility to monitor the value of environment variables which may change 
    /// during the run of an application. Checks the environment variables 
    /// intermittently.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class AppServiceEnvVarMonitor
    {
        // Default list of environment variables tracked by this monitor.
        internal static IReadOnlyCollection<string> PreloadedMonitoredEnvironmentVariables = new string[]
        {
            "WEBSITE_SITE_NAME",
            "WEBSITE_SLOT_NAME",
            "WEBSITE_HOME_STAMPNAME",
            "WEBSITE_HOSTNAME",
            "WEBSITE_OWNER_NAME"
        };

        // how often we allow the code to re-check the environment
        internal readonly TimeSpan checkInterval = TimeSpan.FromSeconds(30);

        // Environment variables tracked by this monitor.
        private readonly ConcurrentDictionary<string, string> CheckedValues;

        // timer object that will periodically update the environment variables
        private readonly Timer environmentCheckTimer;

        // singleton pattern, this is the one instance of this class allowed
        private static readonly AppServiceEnvVarMonitor instance = new AppServiceEnvVarMonitor();

        // event raised whenever any of the environment variables being watched get updated
        public MonitoredEnvironmentVariableUpdated MonitoredEnvironmentVariableUpdatedEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppServiceEnvVarMonitor" /> class.
        /// </summary>
        private AppServiceEnvVarMonitor()
        {
            this.CheckedValues = new ConcurrentDictionary<string, string>();
            this.CheckVariablesIntermittent(null);
            this.environmentCheckTimer = new Timer(this.CheckVariablesIntermittent, null, this.checkInterval, TimeSpan.FromMilliseconds(-1));
        }

        public static AppServiceEnvVarMonitor Instance => AppServiceEnvVarMonitor.instance;

        /// <summary>
        /// Get the latest value assigned to an environment variable.
        /// </summary>
        /// <param name="envVarName">Name of the environment variable to acquire.</param>
        /// <param name="value">Current cached value of the environment variable.</param>
        public void GetCurrentEnvironmentVariableValue(string envVarName, ref string value)
        {
            value = this.CheckedValues.GetOrAdd(envVarName, (key) => { return Environment.GetEnvironmentVariable(key); });
        }

        /// <summary>
        /// Check and update the variables being tracked and if any updates are detected,
        /// raise the OnEnvironmentVariableUpdated event. Restart the timer to check again
        /// in the configured interval once complete.
        /// </summary>
        /// <param name="state">Unused.</param>
        internal void CheckVariablesIntermittent(object state)
        {
            var keys = this.CheckedValues.Keys;
            
            bool shouldTriggerOnUpdate = false;

            foreach (var key in keys)
            {
                string envValue = Environment.GetEnvironmentVariable(key);
                this.CheckedValues.TryGetValue(key, out string lastValue);
                if (!envValue.Equals(lastValue, StringComparison.Ordinal))
                {
                    shouldTriggerOnUpdate = this.CheckedValues.TryUpdate(key, envValue, lastValue) || shouldTriggerOnUpdate;
                }
            }

            if (shouldTriggerOnUpdate)
            {
                OnEnvironmentVariableUpdated();
            }

            this.environmentCheckTimer.Change(this.checkInterval, TimeSpan.FromMilliseconds(-1));
        }

        internal void OnEnvironmentVariableUpdated()
        {
            this.MonitoredEnvironmentVariableUpdatedEvent?.Invoke();
        }
    }
}
