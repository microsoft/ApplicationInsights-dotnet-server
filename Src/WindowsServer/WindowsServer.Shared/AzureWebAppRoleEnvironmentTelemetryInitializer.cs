namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;

    /// <summary>
    /// A telemetry initializer that will gather Azure Web App Role Environment context information.
    /// </summary>
    public sealed class AzureWebAppRoleEnvironmentTelemetryInitializer : ITelemetryInitializer, IDisposable
    {
        /// <summary>
        /// Azure Web App Hostname. This will include the deployment slot, but will be 
        /// same across instances of same slot. Marked as internal to support testing.
        /// </summary>
        internal string WebAppHostNameEnvironmentVariable = "WEBSITE_HOSTNAME";

        /// <summary>Predefined suffix for Azure Web App Hostname.</summary>
        private const string WebAppSuffix = ".azurewebsites.net";

        /// <summary>
        /// Value used for keeping track of when the hostname changes (slot swaps occur). We use this
        /// to notify the other class that makes use of these environment variables.
        /// </summary>
        private string nodeName;
        private string roleName;

        private volatile bool updateEnvVars = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureWebAppRoleEnvironmentTelemetryInitializer" /> class.
        /// </summary>
        public AzureWebAppRoleEnvironmentTelemetryInitializer()
        {
            WindowsServerEventSource.Log.TelemetryInitializerLoaded(this.GetType().FullName);
            AppServiceEnvVarMonitor.Instance.MonitoredAppServiceEnvVarUpdatedEvent += this.UpdateEnvironmentValues;
        }

        /// <summary>
        /// Initializes <see cref="ITelemetry" /> device context and helps keep any heartbeat values in sync as well.
        /// </summary>
        /// <param name="telemetry">The telemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (this.updateEnvVars)
            {
                this.roleName = this.GetRoleName();
                this.nodeName = this.GetNodeName();
                this.updateEnvVars = false;
            }

            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = this.roleName;
            }

            if (string.IsNullOrEmpty(telemetry.Context.GetInternalContext().NodeName))
            {
                telemetry.Context.GetInternalContext().NodeName = this.nodeName;
            }
        }

        /// <summary>
        /// Remove our event handler from the environment variable monitor.
        /// </summary>
        public void Dispose()
        {
            AppServiceEnvVarMonitor.Instance.MonitoredAppServiceEnvVarUpdatedEvent -= this.UpdateEnvironmentValues;
        }

        private string GetRoleName()
        {
            var result = this.GetNodeName();
            if (result.ToLowerInvariant().EndsWith(WebAppSuffix, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(0, result.Length - WebAppSuffix.Length);
            }

            return result;
        }

        private string GetNodeName()
        {
            string nodeName = string.Empty;
            AppServiceEnvVarMonitor.Instance.GetCurrentEnvironmentVariableValue(this.WebAppHostNameEnvironmentVariable, ref nodeName);
            return nodeName ?? string.Empty;
        }

        private void UpdateEnvironmentValues()
        {
            this.updateEnvVars = true;
        }
    }
}
