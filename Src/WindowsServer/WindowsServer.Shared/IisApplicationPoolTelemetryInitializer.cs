namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;

    /// <summary>
    /// A telemetry initializer that will gather IIS application pool context information.
    /// </summary>
    public sealed class IisApplicationPoolTelemetryInitializer : ITelemetryInitializer, IDisposable
    {
        private readonly string iisApplicationPoolNameEnvironmentVariable;
        private readonly string computerNameEnvironmentVariable;
        private string roleName;
        private string roleInstanceName;
        private volatile bool updateEnvVars = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="IisApplicationPoolTelemetryInitializer" /> class.
        /// </summary>
        public IisApplicationPoolTelemetryInitializer()
            : this("APP_POOL_ID", "COMPUTERNAME")
        {
        }

        internal IisApplicationPoolTelemetryInitializer(string iisApplicationPoolNameEnvironmentVariable, string computerNameEnvironmentVariable)
        {
            this.iisApplicationPoolNameEnvironmentVariable = iisApplicationPoolNameEnvironmentVariable;
            this.computerNameEnvironmentVariable = computerNameEnvironmentVariable;
            WindowsServerEventSource.Log.TelemetryInitializerLoaded(this.GetType().FullName);
            AppServiceEnvironmentVariableMonitor.Instance.MonitoredAppServiceEnvVarUpdatedEvent += this.UpdateEnvironmentValues;
        }

        /// <summary>
        /// Initializes <see cref="ITelemetry" /> role and node context information.
        /// </summary>
        /// <param name="telemetry">The telemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (this.updateEnvVars)
            {
                this.roleName = this.GetRoleName();
                this.roleInstanceName = this.GetRoleInstanceName();
                this.updateEnvVars = false;
            }

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

        /// <summary>
        /// Remove our event handler from the environment variable monitor.
        /// </summary>
        public void Dispose()
        {
            AppServiceEnvironmentVariableMonitor.Instance.MonitoredAppServiceEnvVarUpdatedEvent -= this.UpdateEnvironmentValues;
        }

        private string GetRoleName()
        {
            string roleName = string.Empty;
            AppServiceEnvironmentVariableMonitor.Instance.GetCurrentEnvironmentVariableValue(this.iisApplicationPoolNameEnvironmentVariable, ref roleName);
            return roleName ?? string.Empty;
        }

        private string GetRoleInstanceName()
        {
            string roleInstanceName = string.Empty;
            AppServiceEnvironmentVariableMonitor.Instance.GetCurrentEnvironmentVariableValue(this.computerNameEnvironmentVariable, ref roleInstanceName);
            return roleInstanceName ?? string.Empty;
        }

        private void UpdateEnvironmentValues()
        {
            this.updateEnvVars = true;
        }
    }
}
