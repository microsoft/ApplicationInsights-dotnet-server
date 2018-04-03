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
    public class AzureWebAppRoleEnvironmentTelemetryInitializer : ITelemetryInitializer
    {
        /// <summary>Azure Web App Hostname. This will include the deployment slot, but will be same across instances of same slot.</summary>
        private const string WebAppHostNameEnvironmentVariable = "WEBSITE_HOSTNAME";

        /// <summary>Predefined suffix for Azure Web App Hostname.</summary>
        private const string WebAppSuffix = ".azurewebsites.net";

        private string lastNodeValue;
        private AppServiceEnvVarMonitor envVarMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureWebAppRoleEnvironmentTelemetryInitializer" /> class.
        /// </summary>
        public AzureWebAppRoleEnvironmentTelemetryInitializer()
        {
            WindowsServerEventSource.Log.TelemetryInitializerLoaded(this.GetType().FullName);
            this.envVarMonitor = new AppServiceEnvVarMonitor();
        }

        /// <summary>
        /// Initializes <see cref="ITelemetry" /> device context and helps keep any heartbeat values in sync as well.
        /// </summary>
        /// <param name="telemetry">The telemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            string nodeName = string.Empty;

            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = this.GetRoleName();
            }

            nodeName = this.GetNodeName();
            if (string.IsNullOrEmpty(telemetry.Context.GetInternalContext().NodeName))
            {
                telemetry.Context.GetInternalContext().NodeName = nodeName;
            }
            
            // ensure heartbeat values are up to date...
            if (string.IsNullOrEmpty(this.lastNodeValue))
            {
                this.lastNodeValue = nodeName;
            }
            else if (!nodeName.Equals(this.lastNodeValue, StringComparison.Ordinal))
            {
                // if the AppServices heartbeat telemetry module exists, signal it to update the values in heartbeat
                AppServicesHeartbeatTelemetryModule.Instance?.UpdateHeartbeatWithAppServiceEnvVarValues();
                this.lastNodeValue = nodeName;
            }
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
            this.envVarMonitor.GetUpdatedEnvironmentVariable(WebAppHostNameEnvironmentVariable, ref nodeName);
            return nodeName ?? string.Empty;
        }
    }
}
