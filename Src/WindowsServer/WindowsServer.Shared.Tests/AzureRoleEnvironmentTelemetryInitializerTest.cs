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
    using System.IO;
#if NET45
    using System.Diagnostics.Tracing;
#endif
    using Web.TestFramework;
#if NET40
    using Microsoft.Diagnostics.Tracing;
#endif

    [TestClass]
    public class AzureRoleEnvironmentTelemetryInitializerTest
    {        
        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerDoesNotOverrideRoleName()
        {
            var telemetryItem = new EventTelemetry();            
            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            telemetryItem.Context.Cloud.RoleName = "Test";
            initializer.Initialize(telemetryItem);

            Assert.Equal("Test", telemetryItem.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerDoesNotOverrideRoleInstance()
        {
            var telemetryItem = new EventTelemetry();            
            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            telemetryItem.Context.Cloud.RoleInstance = "Test";
            initializer.Initialize(telemetryItem);

            Assert.Equal("Test", telemetryItem.Context.Cloud.RoleInstance);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerDoesNotOverrideNodeName()
        {
            var telemetryItem = new EventTelemetry();            
            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            telemetryItem.Context.GetInternalContext().NodeName = "Test";
            initializer.Initialize(telemetryItem);

            Assert.Equal("Test", telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        public void AzureRoleEnvironmentTelemetryInitializerSetsTelemetryContextPropertiesToNullWhenNotRunningInsideAzureCloudService()
        {            
            // This test asssumes that it is not running inside a cloud service.
            // Its Ok even if Azure ServiceRunTime dlls are in the GAC, as IsAvailable() will return false, and hence 
            // no context will be further attempted to be read.
            var telemetryItem = new EventTelemetry();            
            AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();
            initializer.Initialize(telemetryItem);            

            Assert.Null(telemetryItem.Context.Cloud.RoleName);
            Assert.Null(telemetryItem.Context.Cloud.RoleInstance);
            Assert.Null(telemetryItem.Context.GetInternalContext().NodeName);
        }

        [TestMethod]
        [Description("Validates that requested DLL was loaded into separate AppDomain and not to the current domain.")]
        public void AzureRoleEnvironmentTelemetryInitializerLoadDllToSeparateAppDomain()
        {
            // A random dll which is not already loaded to the current AppDomain but dropped into bin folder.
            string dllPath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "Newtonsoft.Json.dll");
                        
            try
            {
                // Publish the dll to GAC to give a chance for  AzureRoleEnvironmentTelemetryInitializer to load it to a new AppDomain
                new System.EnterpriseServices.Internal.Publish().GacInstall(dllPath);

                // Validate that the dll is not loaded to test AppDomaion to begin with.
                var retrievedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => string.Equals(item.GetName().Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase));
                Assert.Null(retrievedAssembly);

                using (var listener = new TestEventListener())
                {
                    const long AllKeyword = -1;
                    listener.EnableEvents(WindowsServerEventSource.Log, EventLevel.Verbose, (EventKeywords)AllKeyword);

                    // Create initializer - this will internally create separate appdomain and load assemblies into it.
                    AzureRoleEnvironmentContextReader.BaseDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                    AzureRoleEnvironmentContextReader.AssemblyName = "Newtonsoft.Json";
                    AzureRoleEnvironmentContextReader.Culture = "neutral";
                    AzureRoleEnvironmentContextReader.PublicKeyToken = "30ad4fe6b2a6aeed";
                    AzureRoleEnvironmentContextReader.VersionsToAttempt = new string[] { "2.7.0.0", "8.0.0.0" };
                    AzureRoleEnvironmentTelemetryInitializer initializer = new AzureRoleEnvironmentTelemetryInitializer();

                    // Validate that the dll is still not loaded to current appdomain.
                    retrievedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => string.Equals(item.GetName().Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase));
                    Assert.Null(retrievedAssembly);


                    // Validate that Assembly was indeed loaded into separate AppDomain by checking if success message is logged to EventLog.
                    bool messageFound = false;
                    string expectedMessage = "loaded assembly from remote worker in separate AppDomain";
                    foreach (var actualEvent in listener.Messages.Where((arg) => { return arg.Level == EventLevel.Verbose; }))
                    {
                        string actualMessage = string.Format(CultureInfo.InvariantCulture, actualEvent.Message, actualEvent.Payload.ToArray());
                        if(actualMessage.Contains(expectedMessage))
                        {
                            messageFound = true;
                            break;
                        }
                    }
                    Assert.True(messageFound);
                }                
            }
            finally
            {
                new System.EnterpriseServices.Internal.Publish().GacRemove(dllPath);
            }
        }
    }
}
