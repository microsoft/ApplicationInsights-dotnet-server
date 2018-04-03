namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    internal delegate void MonitoredAppServiceEnvVarUpdated();

    /// <summary>
    /// Utility to monitor the value of environment variables which may change 
    /// during the run of an application. Checks the environment variables 
    /// intermittently.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class AppServiceEnvVarMonitor : EnvironmentVariableMonitor
    {
        // event raised whenever any of the environment variables being watched get updated
        public MonitoredAppServiceEnvVarUpdated MonitoredAppServiceEnvVarUpdatedEvent;

        // Default interval between environment variable checks
        internal static TimeSpan MonitorInterval = TimeSpan.FromSeconds(30);

        // Default list of environment variables tracked by this monitor.
        internal static IReadOnlyCollection<string> PreloadedMonitoredEnvironmentVariables = new string[]
        {
            "WEBSITE_SITE_NAME",
            "WEBSITE_SLOT_NAME",
            "WEBSITE_HOME_STAMPNAME",
            "WEBSITE_HOSTNAME",
            "WEBSITE_OWNER_NAME"
        };

        // singleton pattern, this is the one instance of this class allowed
        private static readonly AppServiceEnvVarMonitor SingletonInstance = new AppServiceEnvVarMonitor();

        /// <summary>
        /// Prevents a default instance of the <see cref="AppServiceEnvVarMonitor" /> class from being created.
        /// </summary>
        private AppServiceEnvVarMonitor() : 
            base(
                AppServiceEnvVarMonitor.PreloadedMonitoredEnvironmentVariables, 
                AppServiceEnvVarMonitor.MonitorInterval)
        {
        }

        public static AppServiceEnvVarMonitor Instance => AppServiceEnvVarMonitor.SingletonInstance;

        protected override void OnEnvironmentVariableUpdated()
        {
            this.MonitoredAppServiceEnvVarUpdatedEvent?.Invoke();
        }
    }
}
