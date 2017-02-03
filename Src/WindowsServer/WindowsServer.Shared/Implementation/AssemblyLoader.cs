namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;

    /// <summary>
    /// AssemblyLoader contains logic to load a given assembly and read properties using reflection.
    /// Inherits MarshalByRefObject so that methods of this class can be executed remotely in separate AppDomain.
    /// </summary>    
    internal class AssemblyLoader : MarshalByRefObject
    {
        public string AssemblyName;
        public string Culture;
        public string PublicKeyToken;
        public string[] VersionsToAttempt;

        public bool ReadAndPopulateContextInformation(ref string roleName, ref string roleInstanceId)
        {
            Assembly loadedAssembly = null;
            try
            {
                // As this is executed inside a separate AppDomain, it is safe to load assemblies here without interfering with user code.                
                loadedAssembly = this.AttemptToLoadAssembly(this.AssemblyName, this.Culture, this.PublicKeyToken, this.VersionsToAttempt);
                if (loadedAssembly != null)
                {
                    ServiceRuntime serviceRuntime = new ServiceRuntime();
                    RoleEnvironment roleEnvironment = serviceRuntime.GetRoleEnvironment(loadedAssembly);

                    if (roleEnvironment.IsAvailable == true)
                    {
                        RoleInstance roleInstance = roleEnvironment.CurrentRoleInstance;
                        if (roleInstance != null)
                        {
                            roleInstanceId = roleInstance.Id;
                            Role role = roleInstance.Role;
                            roleName = role.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WindowsServerEventSource.Log.TroubleshootingMessageEvent("Unknown error occured attempting to populate Azure Context, Exception: " + ex.ToString());
            }

            return loadedAssembly != null;
        }

        private Assembly AttemptToLoadAssembly(string assemblyName, string culture, string publicKeyToken, string[] versionsToAttempt)
        {
            Assembly loadedAssembly = null;
            string assemblyNameFormat = "{0}, Version={1}, Culture={2}, PublicKeyToken={3}";
            string assemblyNameFormatVersion = string.Format(CultureInfo.InvariantCulture, assemblyNameFormat, assemblyName, "{0}", culture, publicKeyToken);
            //// An example of the above string contents at this point: "Microsoft.WindowsAzure.ServiceRuntime, Version={0}, Culture=neutral, PublicKeyToken=31bf3856ad364e35";

            foreach (string version in versionsToAttempt)
            {
                string fullyQualifiedAssemblyName = string.Format(CultureInfo.InvariantCulture, assemblyNameFormatVersion, version);
                try
                {
                    loadedAssembly = Assembly.Load(fullyQualifiedAssemblyName);
                    if (loadedAssembly != null)
                    {
                        // Found the assembly, stop probing and return the assembly.
                        WindowsServerEventSource.Log.TroubleshootingMessageEvent(string.Format(CultureInfo.InvariantCulture, "Successfully Loaded {0} from location: {1} ", fullyQualifiedAssemblyName, loadedAssembly.Location));
                        return loadedAssembly;
                    }
                }
                catch (Exception ex)
                {
                    WindowsServerEventSource.Log.TroubleshootingMessageEvent(string.Format(CultureInfo.InvariantCulture, "Failed Loading {0} with exception: {1} ", fullyQualifiedAssemblyName, ex.Message));
                }
            }

            // Failed to load assembly.
            WindowsServerEventSource.Log.TroubleshootingMessageEvent("Failed to find any supported versions of " + assemblyName);
            return loadedAssembly;
        }
    }
}
