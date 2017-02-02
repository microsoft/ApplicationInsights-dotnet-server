namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System.Globalization;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    using Microsoft.ApplicationInsights.WindowsServer.Azure;
    using Microsoft.ApplicationInsights.WindowsServer.Azure.Emulation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;
    using System;
    using System.Linq;

    [TestClass]
    public class AzureRoleEnvironmentTelemetryInitializerTest
    {
        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerSetsTelemetryContextPropertiesWhenRoleEnvironmentIsAvailable()
        {
            var telemetryItem = new EventTelemetry();
            AzureRoleEnvironmentContextReader.BaseDirectory = ServiceRuntimeHelper.TestWithServiceRuntimePath;
            AzureRoleEnvironmentContextReader.Instance = null;

            ServiceRuntimeHelper.IsAvailable = true;

            var initializer = new AzureRoleEnvironmentTelemetryInitializer();
            initializer.Initialize(telemetryItem);

            string expectedRoleInstanceName = string.Format(
                                    CultureInfo.InvariantCulture,
                                    TestRoleInstance.IdFormat,
                                    ServiceRuntimeHelper.RoleName,
                                    ServiceRuntimeHelper.RoleInstanceOrdinal);

            Assert.Equal(ServiceRuntimeHelper.RoleName, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(expectedRoleInstanceName, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(expectedRoleInstanceName, telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerDoesNotOverrideRoleName()
        {
            var telemetryItem = new EventTelemetry();
            AzureRoleEnvironmentContextReader.BaseDirectory = ServiceRuntimeHelper.TestWithServiceRuntimePath;
            AzureRoleEnvironmentContextReader.Instance = null;
            ServiceRuntimeHelper.IsAvailable = true;

            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            telemetryItem.Context.Cloud.RoleName = "Test";
            initializer.Initialize(telemetryItem);

            Assert.Equal("Test", telemetryItem.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerDoesNotOverrideRoleInstance()
        {
            var telemetryItem = new EventTelemetry();
            AzureRoleEnvironmentContextReader.BaseDirectory = ServiceRuntimeHelper.TestWithServiceRuntimePath;
            AzureRoleEnvironmentContextReader.Instance = null;
            ServiceRuntimeHelper.IsAvailable = true;

            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            telemetryItem.Context.Cloud.RoleInstance = "Test";
            initializer.Initialize(telemetryItem);

            Assert.Equal("Test", telemetryItem.Context.Cloud.RoleInstance);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerDoesNotOverrideNodeName()
        {
            var telemetryItem = new EventTelemetry();
            AzureRoleEnvironmentContextReader.BaseDirectory = ServiceRuntimeHelper.TestWithServiceRuntimePath;
            AzureRoleEnvironmentContextReader.Instance = null;
            ServiceRuntimeHelper.IsAvailable = true;

            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            telemetryItem.Context.GetInternalContext().NodeName = "Test";
            initializer.Initialize(telemetryItem);

            Assert.Equal("Test", telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerSetsTelemetryContextPropertiesWhenRoleEnvironmentIsNotAvailable()
        {
            var telemetryItem = new EventTelemetry();
            AzureRoleEnvironmentContextReader.BaseDirectory = ServiceRuntimeHelper.TestWithServiceRuntimePath;
            AzureRoleEnvironmentContextReader.Instance = null;
            ServiceRuntimeHelper.IsAvailable = false;

            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            initializer.Initialize(telemetryItem);
            ServiceRuntimeHelper.IsAvailable = true;

            Assert.Null(telemetryItem.Context.Cloud.RoleName);
            Assert.Null(telemetryItem.Context.Cloud.RoleInstance);
            Assert.Null(telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerLoadDllToSeparateAppDomain()
        {
            new System.EnterpriseServices.Internal.Publish().GacInstall(@"D:\Newtonsoft.Json.dll");

            // Validate that Microsoft.WindowsAzure.ServiceRuntime is not loaded to begin with.
            var srtAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => string.Equals(item.GetName().Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase));
            Assert.Null(srtAssembly);

            // Create initializer - this will internally create separate appdomain and load assemblies into it.
            AzureRoleEnvironmentContextReader.BaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            AzureRoleEnvironmentContextReader.AssemblyName = "Newtonsoft.Json";
            AzureRoleEnvironmentContextReader.Culture = "neutral";
            AzureRoleEnvironmentContextReader.PublicKeyToken = "30ad4fe6b2a6aeed";
            AzureRoleEnvironmentContextReader.VersionsToAttempt = new string[] { "2.7.0.0", "8.0.0.0"};
            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();

            // Validate that Microsoft.WindowsAzure.ServiceRuntime is not loaded to current appdomain
            srtAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => string.Equals(item.GetName().Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase));
            Assert.Null(srtAssembly);
                        
            new System.EnterpriseServices.Internal.Publish().GacRemove(@"D:\Newtonsoft.Json.dll");

        }
    }
}
