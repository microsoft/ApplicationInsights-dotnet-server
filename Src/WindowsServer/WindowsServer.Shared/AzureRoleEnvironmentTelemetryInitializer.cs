namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System.Threading;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;    

    /// <summary>
    /// A telemetry initializer that will gather Azure Role Environment context information.
    /// </summary>
    public class AzureRoleEnvironmentTelemetryInitializer : ITelemetryInitializer
    {
        private string roleInstanceName;
        private string roleName;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureRoleEnvironmentTelemetryInitializer" /> class.
        /// </summary>
        public AzureRoleEnvironmentTelemetryInitializer()
        {
            WindowsServerEventSource.Log.TelemetryInitializerLoaded(this.GetType().FullName);

            try
            {
                this.roleName = AzureRoleEnvironmentContextReader.Instance.GetRoleName();
                this.roleInstanceName = AzureRoleEnvironmentContextReader.Instance.GetRoleInstanceName();
            }
            catch(System.Exception ex)
            {
                WindowsServerEventSource.Log.TroubleshootingMessageEvent("AzureRoleEnvironmentTelemetryInitializer creation failed with:" + ex.ToString());
            }
        }

        /// <summary>
        /// Initializes <see cref="ITelemetry" /> device context.
        /// </summary>
        /// <param name="telemetry">The telemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {                
                telemetry.Context.Cloud.RoleName = this.roleName;
            }

            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleInstance))
            {                
                telemetry.Context.Cloud.RoleInstance = this.roleInstanceName;
            }

            if (string.IsNullOrEmpty(telemetry.Context.GetInternalContext().NodeName))
            {                
                telemetry.Context.GetInternalContext().NodeName = this.roleInstanceName;
            }
        }
    }
}
