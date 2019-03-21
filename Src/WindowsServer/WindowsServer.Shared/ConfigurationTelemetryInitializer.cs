using System;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.WindowsServer.Implementation;

namespace Microsoft.ApplicationInsights.WindowsServer
{
    /// <summary>
    /// A telemetry initializer that will gather configuration information.
    /// </summary>
    public class ConfigurationTelemetryInitializer : ITelemetryInitializer
    {
        private string roleName;
        private string roleInstanceName;
        private volatile bool updateEnvVars = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationTelemetryInitializer" /> class.
        /// </summary>
        public ConfigurationTelemetryInitializer()
        {
            WindowsServerEventSource.Log.TelemetryInitializerLoaded(this.GetType().FullName);
            AppServiceEnvironmentVariableMonitor.Instance.MonitoredAppServiceEnvVarUpdatedEvent += this.UpdateEnvironmentValues;
        }

        /// <summary>
        /// Gets or sets the <c>cloud_RoleName</c>.
        /// </summary>
        /// <value>The <c>cloud_RoleName</c>.</value>
        /// <remarks>Supports environment variable expansion with <c>%VARIABLE_NAME%</c></remarks>
        public string RoleName { get; set; }

        /// <summary>
        /// Gets or sets the <c>cloud_RoleInstance</c>.
        /// </summary>
        /// <value>The <c>cloud_RoleInstance</c>.</value>
        /// <remarks>Supports environment variable expansion with <c>%VARIABLE_NAME%</c></remarks>
        public string RoleInstance { get; set; }

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

            if (this.roleName is string && string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = this.roleName;
            }

            if (this.roleInstanceName is string && string.IsNullOrEmpty(telemetry.Context.Cloud.RoleInstance))
            {
                telemetry.Context.Cloud.RoleInstance = this.roleInstanceName;
            }

            if (this.roleInstanceName is string && string.IsNullOrEmpty(telemetry.Context.GetInternalContext().NodeName))
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

        private string GetRoleName() => ExpandEnvironmentVariables(this.RoleName);

        private string GetRoleInstanceName() => ExpandEnvironmentVariables(this.RoleInstance);

        private static string ExpandEnvironmentVariables(string definition)
        {
            if (string.IsNullOrWhiteSpace(definition))
            {
                return default(string);
            }

            var expandedDefinition = Regex.Replace(definition, @"(?<e>%%)|%(?<v>[^%]+)%", ExpandEnvironmentVariable, RegexOptions.ExplicitCapture);

            return expandedDefinition;
        }

        private static string ExpandEnvironmentVariable(Match match)
        {
            var variableGroup = match.Groups["v"];

            if (variableGroup.Success)
            {
                var value = default(string);
                AppServiceEnvironmentVariableMonitor.Instance.GetCurrentEnvironmentVariableValue(variableGroup.Value, ref value);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            return null;
        }

        private void UpdateEnvironmentValues()
        {
            this.updateEnvVars = true;
        }
    }
}
