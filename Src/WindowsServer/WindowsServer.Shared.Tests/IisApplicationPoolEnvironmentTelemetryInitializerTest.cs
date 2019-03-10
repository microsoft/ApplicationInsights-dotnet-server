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
    public class IisApplicationPoolEnvironmentTelemetryInitializerTest
    {
        private const string RoleName = "TestRoleName";
        private const string ComputerName = "TestComputerName";

        [TestMethod]
        public void IisApplicationPoolTelemetryInitializerSetsRoleName()
        {
            string appPoolTestVarName, computerTestVarName;
            GetAndInitializeEnvironment(out appPoolTestVarName, out computerTestVarName);

            var telemetryItem = new EventTelemetry();

            var initializer = new IisApplicationPoolTelemetryInitializer(appPoolTestVarName, computerTestVarName);

            initializer.Initialize(telemetryItem);

            Assert.Equal(RoleName, telemetryItem.Context.Cloud.RoleName);

            ClearEnvironmentVariables(appPoolTestVarName, computerTestVarName);
        }

        [TestMethod]
        public void IisApplicationPoolTelemetryInitializerSetsRoleInstanceNameAndNodeName()
        {
            string appPoolTestVarName, computerTestVarName;
            GetAndInitializeEnvironment(out appPoolTestVarName, out computerTestVarName);

            var telemetryItem = new EventTelemetry();

            var initializer = new IisApplicationPoolTelemetryInitializer(appPoolTestVarName, computerTestVarName);

            initializer.Initialize(telemetryItem);

            Assert.Equal(ComputerName, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(ComputerName, telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(appPoolTestVarName, computerTestVarName);
        }

        [TestMethod]
        public void IisApplicationPoolTelemetryInitializerDoesNotOverrideRoleName()
        {
            const string TestValue = "Test";
            string appPoolTestVarName, computerTestVarName;
            GetAndInitializeEnvironment(out appPoolTestVarName, out computerTestVarName);

            var telemetryItem = new EventTelemetry();
            telemetryItem.Context.Cloud.RoleName = TestValue;

            var initializer = new IisApplicationPoolTelemetryInitializer(appPoolTestVarName, computerTestVarName);

            initializer.Initialize(telemetryItem);

            Assert.Equal(TestValue, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(ComputerName, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(ComputerName, telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(appPoolTestVarName, computerTestVarName);
        }

        [TestMethod]
        public void IisApplicationPoolTelemetryInitializerDoesNotOverrideRoleInstance()
        {
            const string TestValue = "Test";
            string appPoolTestVarName, computerTestVarName;
            GetAndInitializeEnvironment(out appPoolTestVarName, out computerTestVarName);

            var telemetryItem = new EventTelemetry();
            telemetryItem.Context.Cloud.RoleInstance = TestValue;

            var initializer = new IisApplicationPoolTelemetryInitializer(appPoolTestVarName, computerTestVarName);

            initializer.Initialize(telemetryItem);

            Assert.Equal(RoleName, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(TestValue, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(ComputerName, telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(appPoolTestVarName, computerTestVarName);
        }

        [TestMethod]
        public void IisApplicationPoolTelemetryInitializerDoesNotOverrideNodeName()
        {
            const string TestValue = "Test";
            string appPoolTestVarName, computerTestVarName;
            GetAndInitializeEnvironment(out appPoolTestVarName, out computerTestVarName);

            var telemetryItem = new EventTelemetry();
            telemetryItem.Context.GetInternalContext().NodeName = TestValue;

            var initializer = new IisApplicationPoolTelemetryInitializer(appPoolTestVarName, computerTestVarName);

            initializer.Initialize(telemetryItem);

            Assert.Equal(RoleName, telemetryItem.Context.Cloud.RoleName);
            Assert.Equal(ComputerName, telemetryItem.Context.Cloud.RoleInstance);
            Assert.Equal(TestValue, telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(appPoolTestVarName, computerTestVarName);
        }

        [TestMethod]
        public void IisApplicationPoolTelemetryInitializerEmptyVariable()
        {
            string appPoolTestVarName, computerTestVarName;
            GetAndInitializeEnvironment(out appPoolTestVarName, out computerTestVarName);
            Environment.SetEnvironmentVariable(appPoolTestVarName, null);
            Environment.SetEnvironmentVariable(computerTestVarName, null);

            var telemetryItem = new EventTelemetry();

            var initializer = new IisApplicationPoolTelemetryInitializer(appPoolTestVarName, computerTestVarName);

            initializer.Initialize(telemetryItem);

            Assert.Null(telemetryItem.Context.Cloud.RoleName);
            Assert.Null(telemetryItem.Context.Cloud.RoleInstance);
            Assert.Null(telemetryItem.Context.GetInternalContext().NodeName);

            ClearEnvironmentVariables(appPoolTestVarName, computerTestVarName);
        }

        private static void GetAndInitializeEnvironment(out string appPoolVarName, out string computerVarName)
        {
            appPoolVarName = "APP_POOL_ID_" + Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(appPoolVarName, RoleName);

            computerVarName = "COMPUTERNAME_" + Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(computerVarName, ComputerName);
        }

        private static void ClearEnvironmentVariables(string appPoolVarName, string computerVarName)
        {
            Environment.SetEnvironmentVariable(appPoolVarName, null);

            Environment.SetEnvironmentVariable(computerVarName, null);
        }
    }
}
