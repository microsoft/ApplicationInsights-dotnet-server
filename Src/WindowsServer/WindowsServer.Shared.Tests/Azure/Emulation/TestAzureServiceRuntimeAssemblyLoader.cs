namespace Microsoft.ApplicationInsights.WindowsServer.Azure.Emulation
{
    using System;
    using System.Globalization;
    using System.Reflection;        
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;

    /// <summary>
    /// Used for testing AzureServiceRuntimeAssemblyLoader. Loads a random assembly and if successful populates
    /// context with test values.
    /// </summary>
    internal class TestAzureServiceRuntimeAssemblyLoader : AzureServiceRuntimeAssemblyLoader
    {
        public TestAzureServiceRuntimeAssemblyLoader()
        {
            // Loads a random assembly. (only requirement is that this assembly is foundable in GAC)
            this.AssemblyNameToLoad.Name = "Newtonsoft.Json";
            this.AssemblyNameToLoad.CultureInfo = CultureInfo.InvariantCulture;
            this.AssemblyNameToLoad.SetPublicKeyToken(new byte[] { 48, 173, 79, 230, 178, 166, 174, 237 });
            this.VersionsToAttempt = new Version[] { new Version("2.0.0.0"), new Version("8.0.0.0") };            
        }

        public override bool ReadAndPopulateContextInformation(out string roleName, out string roleInstanceId)
        {
            roleName = string.Empty;
            roleInstanceId = string.Empty;

            Assembly loadedAssembly = null;
            try
            {
                // As this is executed inside a separate AppDomain, it is safe to load assemblies here without interfering with user code.                
                loadedAssembly = this.AttemptToLoadAssembly(this.AssemblyNameToLoad, this.VersionsToAttempt);
                if (loadedAssembly != null)
                {
                    // This is a test loader. Just populate test values if assembly loading is successfull.
                    roleName = "TestRoleName";
                    roleInstanceId = "TestRoleInstanceId";
                }
            }
            catch (Exception ex)
            {
                WindowsServerEventSource.Log.UnknownErrorOccured("TestAzureServiceRuntimeAssemblyLoader populate context", ex.ToString());
            }

            return loadedAssembly != null;
        }
    }
}
