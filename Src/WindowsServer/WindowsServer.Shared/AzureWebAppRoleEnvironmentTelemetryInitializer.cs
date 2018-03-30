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

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureWebAppRoleEnvironmentTelemetryInitializer" /> class.
        /// </summary>
        public AzureWebAppRoleEnvironmentTelemetryInitializer()
        {
            WindowsServerEventSource.Log.TelemetryInitializerLoaded(this.GetType().FullName);
        }

        /// <summary>
        /// Initializes <see cref="ITelemetry" /> device context.
        /// </summary>
        /// <param name="telemetry">The telemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = this.GetRoleName();
            }

            if (string.IsNullOrEmpty(telemetry.Context.GetInternalContext().NodeName))
            {
                telemetry.Context.GetInternalContext().NodeName = this.GetNodeName();
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
            AppServiceEnvVarMonitor.GetUpdatedEnvironmentVariable(WebAppHostNameEnvironmentVariable, ref nodeName);
            return nodeName;
        }
    }
}
