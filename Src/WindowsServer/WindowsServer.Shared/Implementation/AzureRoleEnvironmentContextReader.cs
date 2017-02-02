namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading;

    internal class AzureRoleEnvironmentContextReader : IAzureRoleEnvironmentContextReader
    {
        /// <summary>
        /// The singleton instance for our reader.
        /// </summary>
        private static IAzureRoleEnvironmentContextReader instance;

        /// <summary>
        /// The Azure role name (if any).
        /// </summary>
        private string roleName = string.Empty;

        /// <summary>
        /// The Azure role instance name (if any).
        /// </summary>
        private string roleInstanceName = string.Empty;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureRoleEnvironmentContextReader"/> class.
        /// </summary>
        internal AzureRoleEnvironmentContextReader()
        {
        }

        /// <summary>
        /// Gets or sets the singleton instance for our application context reader.
        /// </summary>
        public static IAzureRoleEnvironmentContextReader Instance
        {
            get
            {
                if (AzureRoleEnvironmentContextReader.instance != null)
                {
                    return AzureRoleEnvironmentContextReader.instance;
                }

                if (string.IsNullOrEmpty(AzureRoleEnvironmentContextReader.BaseDirectory) == true)
                {
                    AzureRoleEnvironmentContextReader.BaseDirectory = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "bin");
                }

                if (string.IsNullOrEmpty(AzureRoleEnvironmentContextReader.AssemblyName) == true)
                {
                    AzureRoleEnvironmentContextReader.AssemblyName = "Microsoft.WindowsAzure.ServiceRuntime";
                }

                if (string.IsNullOrEmpty(AzureRoleEnvironmentContextReader.Culture) == true)
                {
                    AzureRoleEnvironmentContextReader.Culture = "neutral";
                }

                if (string.IsNullOrEmpty(AzureRoleEnvironmentContextReader.PublicKeyToken) == true)
                {
                    AzureRoleEnvironmentContextReader.PublicKeyToken = "31bf3856ad364e35";
                }

                if (AzureRoleEnvironmentContextReader.VersionsToAttempt.Length == 0)
                {
                    AzureRoleEnvironmentContextReader.VersionsToAttempt = new string[] { "2.7.0.0", "2.6.0.0", "2.5.0.0", "2.4.0.0", "2.3.0.0", "2.2.0.0", "2.1.0.0", "2.8.0.0", "2.9.0.0" };
                }
                    
                Interlocked.CompareExchange(ref AzureRoleEnvironmentContextReader.instance, new AzureRoleEnvironmentContextReader(), null);
                AzureRoleEnvironmentContextReader.instance.Initialize();
                return AzureRoleEnvironmentContextReader.instance;
            }

            // allow for the replacement for the context reader to allow for testability
            internal set
            {
                AzureRoleEnvironmentContextReader.instance = value;
            }
        }

        /// <summary>
        /// Gets or sets the base directly where hunting for application DLLs is to start.
        /// </summary>
        internal static string BaseDirectory { get; set; }

        internal static string AssemblyName { get; set; }
        internal static string Culture { get; set; }
        internal static string PublicKeyToken { get; set; }
        internal static string[] VersionsToAttempt { get; set; }

        /// <summary>
        /// Initializes the current reader with respect to its environment.
        /// </summary>
        public void Initialize()
        {
            AppDomain tempDomainToLoadAssembly = null;
            string tempDomainName = "AppInsightsDomain-" + Guid.NewGuid().ToString().Substring(0, 6);
            long beginTimeInTicks = Stopwatch.GetTimestamp();

            // The following approach is used to load Microsoft.WindowsAzure.ServiceRuntime assembly and read the required information.
            // Create a new AppDomain and try to load the ServiceRuntime dll into it.
            // Then using reflection, read and save all the properties we care about and unload the new AppDomain.            
            // This approach ensures that if the app is running in Azure Cloud Service, we read the necessary information deterministically
            // and without interfering with any customer code which could be loading same/different version of Microsoft.WindowsAzure.ServiceRuntime.dll.
            try
            {
                AppDomainSetup domaininfo = new AppDomainSetup();            
                domaininfo.ApplicationBase = AzureRoleEnvironmentContextReader.BaseDirectory;                

                // Create a new AppDomain
                tempDomainToLoadAssembly = AppDomain.CreateDomain(tempDomainName, null, domaininfo);

                // Load the RemoteWorker assembly to the new domain            
                tempDomainToLoadAssembly.Load(typeof(Worker).Assembly.FullName);

                // Any method invoked on this object will be executed in the newly created AppDomain.
                Worker remoteWorker = (Worker)tempDomainToLoadAssembly.CreateInstanceAndUnwrap(typeof(Worker).Assembly.FullName, typeof(Worker).FullName);
                remoteWorker.assemblyName = AzureRoleEnvironmentContextReader.AssemblyName;
                remoteWorker.culture = AzureRoleEnvironmentContextReader.Culture;
                remoteWorker.publicKeyToken = AzureRoleEnvironmentContextReader.PublicKeyToken;
                remoteWorker.versionsToAttempt = AzureRoleEnvironmentContextReader.VersionsToAttempt;
                remoteWorker.ReadAndPopulateContextInformation(ref this.roleName, ref this.roleInstanceName);
            }
            catch(Exception ex)
            {
                WindowsServerEventSource.Log.TroubleshootingMessageEvent("AzureRoleEnvironmentContextReader Initialize failed : " + ex.ToString());
            }
            finally
            {                
                try
                {
                    if(tempDomainToLoadAssembly != null)
                    {
                        AppDomain.Unload(tempDomainToLoadAssembly);
                        long endTimeInTicks = Stopwatch.GetTimestamp();
                        long stopWatchTicksDiff = endTimeInTicks - beginTimeInTicks;
                        double durationInMillisecs = stopWatchTicksDiff * 1000 / (double)Stopwatch.Frequency;                        
                        WindowsServerEventSource.Log.TroubleshootingMessageEvent(tempDomainName + " AppDomain  Unloaded.");
                        WindowsServerEventSource.Log.TroubleshootingMessageEvent("TimeTaken for initialization in msec:" + durationInMillisecs);
                    }                    
                }
                catch(Exception ex)
                {
                    WindowsServerEventSource.Log.TroubleshootingMessageEvent(tempDomainName + " AppDomain  unload failed with exception: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the Azure role name.
        /// </summary>
        /// <returns>The extracted data.</returns>
        public string GetRoleName()
        {
            return this.roleName;
        }

        /// <summary>
        /// Gets the Azure role instance name.
        /// </summary>
        /// <returns>The extracted data.</returns>
        public string GetRoleInstanceName()
        {
            return this.roleInstanceName;
        }
    }

    // Worker contains logic to load Microsoft.WindowsAzure.ServiceRuntime assembly and read properties using reflection
    // Inherits MarshalByRefObject so that methods of this class can be executed remotely in separate AppDomain.
    internal class Worker : MarshalByRefObject
    {
        public string assemblyName;
        public string culture;
        public string publicKeyToken;
        public string[] versionsToAttempt;
        public void ReadAndPopulateContextInformation(ref string roleName, ref string roleInstanceId)
        {                                             
            try
            {
                // As this is executed inside a separate AppDomain, it is safe to load assemblies here without interfering with user code.                
                Assembly loadedAssembly = AttemptToLoadAssembly(assemblyName, culture, publicKeyToken, versionsToAttempt);
                if (loadedAssembly != null)
                {
                    ServiceRuntime serviceRuntime = new ServiceRuntime();
                    RoleEnvironment roleEnvironment = serviceRuntime.GetRoleEnvironment(loadedAssembly, AzureRoleEnvironmentContextReader.BaseDirectory);

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
            catch(Exception ex)
            {
                WindowsServerEventSource.Log.TroubleshootingMessageEvent("Unknown error occured attempting to populate Azure Context, Exception: " + ex.ToString());
            }
        }

        private Assembly AttemptToLoadAssembly(string assemblyName, string culture, string publicKeyToken, string[] versionsToAttempt)
        {
            Assembly loadedAssembly = null;
            //string assemblyNameFormat = "Microsoft.WindowsAzure.ServiceRuntime, Version={0}, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
            string assemblyNameFormat = "{0}, Version={1}, Culture={2}, PublicKeyToken={3}";
            string assemblyNameFormatVersion = string.Format(assemblyNameFormat, assemblyName, "{0}", culture, publicKeyToken);

            // Attempt to read 2.* versions. When 3.* is released, modify the list.            

            foreach(string version in versionsToAttempt)
            {
                try
                {
                    loadedAssembly = Assembly.Load(string.Format(assemblyNameFormatVersion, version));
                    if(loadedAssembly != null)
                    {
                        WindowsServerEventSource.Log.TroubleshootingMessageEvent(string.Format("Successfully Loaded Microsoft.WindowsAzure.ServiceRuntime.dll version {0} from location: {1} ", version, loadedAssembly.Location));
                        return loadedAssembly;
                    }
                }
                catch(Exception ex)
                {
                    WindowsServerEventSource.Log.TroubleshootingMessageEvent(string.Format("Failed Loading Microsoft.WindowsAzure.ServiceRuntime.dll version {0} with exception: {1} ", version, ex.Message));
                }
            }

            // Failed to load assembly.
            WindowsServerEventSource.Log.TroubleshootingMessageEvent("Failed to find any supported versions of Microsoft.WindowsAzure.ServiceRuntime.dll. It is assumed that the application is not run on AzureCloudService.");
            return loadedAssembly;
        }
    }
}
