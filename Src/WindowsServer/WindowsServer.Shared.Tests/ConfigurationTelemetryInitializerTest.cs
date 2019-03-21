namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Globalization;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
#if !NETCORE
    using Microsoft.ApplicationInsights.WindowsServer.Azure;
    using Microsoft.ApplicationInsights.WindowsServer.Azure.Emulation;
#endif
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;

    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class ConfigurationTelemetryInitializerTest
    {
        private const string TestRoleName = nameof(TestRoleName);
        private const string TestRoleInstance = nameof(TestRoleInstance);

        [TestMethod]
        public void ConfigurationTelemetryInitializerSetsRoleName()
        {
            var telemetryItem = new EventTelemetry();

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleName = TestRoleName,
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestRoleName, telemetryItem.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerSetsRoleNameFromEnvironmentVariable()
        {
            string roleNameTestVarName, roleInstanceTestVarName;
            GetAndInitializeEnvironment(out roleNameTestVarName, out roleInstanceTestVarName);

            var telemetryItem = new EventTelemetry();

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleName = $"%{roleNameTestVarName}%",
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestRoleName, telemetryItem.Context.Cloud.RoleName);

            ClearEnvironmentVariables(roleNameTestVarName, roleInstanceTestVarName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerSetsRoleInstanceNameAndNodeName()
        {
            string roleNameTestVarName, roleInstanceTestVarName;
            GetAndInitializeEnvironment(out roleNameTestVarName, out roleInstanceTestVarName);

            var telemetryItem = new EventTelemetry();

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleInstance = TestRoleInstance,
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestRoleInstance, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(TestRoleInstance, telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(roleNameTestVarName, roleInstanceTestVarName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerSetsRoleInstanceNameAndNodeNameFromEnvironmentVariable()
        {
            string roleNameTestVarName, roleInstanceTestVarName;
            GetAndInitializeEnvironment(out roleNameTestVarName, out roleInstanceTestVarName);

            var telemetryItem = new EventTelemetry();

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleInstance = $"%{roleInstanceTestVarName}%",
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestRoleInstance, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(TestRoleInstance, telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(roleNameTestVarName, roleInstanceTestVarName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerDoesNotOverrideRoleName()
        {
            const string TestValue = "Test";

            var telemetryItem = new EventTelemetry();
            telemetryItem.Context.Cloud.RoleName = TestValue;

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleName = TestRoleName,
                RoleInstance = TestRoleInstance,
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestValue, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(TestRoleInstance, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(TestRoleInstance, telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerDoesNotOverrideRoleInstance()
        {
            const string TestValue = "Test";

            var telemetryItem = new EventTelemetry();
            telemetryItem.Context.Cloud.RoleInstance = TestValue;

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleName = TestRoleName,
                RoleInstance = TestRoleInstance,
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestRoleName, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(TestValue, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(TestRoleInstance, telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerDoesNotOverrideNodeName()
        {
            const string TestValue = "Test";

            var telemetryItem = new EventTelemetry();
            telemetryItem.Context.GetInternalContext().NodeName = TestValue;

            var initializer = new ConfigurationTelemetryInitializer
            {
                RoleName = TestRoleName,
                RoleInstance = TestRoleInstance,
            };

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestRoleName, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(TestRoleInstance, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(TestValue, telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void ConfigurationTelemetryInitializerEmptyVariable()
        {
            string roleNameTestVarName, roleInstanceTestVarName;
            GetAndInitializeEnvironment(out roleNameTestVarName, out roleInstanceTestVarName);
            ClearEnvironmentVariables(roleNameTestVarName, roleInstanceTestVarName);

            var telemetryItem = new EventTelemetry();

            var initializer = new ConfigurationTelemetryInitializer();

            initializer.Initialize(telemetryItem);

            Assert.Null(telemetryItem.Context.Cloud.RoleName);
            Assert.Null(telemetryItem.Context.Cloud.RoleInstance);
            Assert.Null(telemetryItem.Context.GetInternalContext().NodeName);
        }

        private static void GetAndInitializeEnvironment(out string roleNameTestVarName, out string roleInstanceTestVarName)
        {
            roleNameTestVarName = "ROLE_NAME_" + Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(roleNameTestVarName, TestRoleName);

            roleInstanceTestVarName = "ROLE_INSTANCE_" + Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(roleInstanceTestVarName, TestRoleInstance);
        }

        private static void ClearEnvironmentVariables(string roleNameTestVarName, string roleInstanceTestVarName)
        {
            Environment.SetEnvironmentVariable(roleNameTestVarName, null);

            Environment.SetEnvironmentVariable(roleInstanceTestVarName, null);
        }
    }
}
