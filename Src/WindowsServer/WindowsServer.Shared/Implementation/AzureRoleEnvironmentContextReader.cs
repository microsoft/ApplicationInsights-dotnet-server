namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
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

                if (AzureRoleEnvironmentContextReader.VersionsToAttempt == null || AzureRoleEnvironmentContextReader.VersionsToAttempt.Length == 0)
                {
                    // This list should be explicity updated when 3.* versions of Microsoft.WindowsAzure.ServiceRuntime are released after confirming they don't have breaking API changes.
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
                tempDomainToLoadAssembly.Load(typeof(AssemblyLoader).Assembly.FullName);

                // Any method invoked on this object will be executed in the newly created AppDomain.
                AssemblyLoader remoteWorker = (AssemblyLoader)tempDomainToLoadAssembly.CreateInstanceAndUnwrap(typeof(AssemblyLoader).Assembly.FullName, typeof(AssemblyLoader).FullName);
                remoteWorker.AssemblyName = AzureRoleEnvironmentContextReader.AssemblyName;
                remoteWorker.Culture = AzureRoleEnvironmentContextReader.Culture;
                remoteWorker.PublicKeyToken = AzureRoleEnvironmentContextReader.PublicKeyToken;
                remoteWorker.VersionsToAttempt = AzureRoleEnvironmentContextReader.VersionsToAttempt;
                bool success = remoteWorker.ReadAndPopulateContextInformation(ref this.roleName, ref this.roleInstanceName);
                if (success)
                {
                    WindowsServerEventSource.Log.TroubleshootingMessageEvent("Successfully loaded assembly from remote worker in separate AppDomain");
                }
                else
                {
                    WindowsServerEventSource.Log.TroubleshootingMessageEvent("Failed loading assembly from remote worker in separate AppDomain. Application is assumed not be running in Azure Cloud service.");
                }
            }
            catch (Exception ex)
            {
                WindowsServerEventSource.Log.TroubleshootingMessageEvent("AzureRoleEnvironmentContextReader Initialize failed : " + ex.ToString());
            }
            finally
            {                
                try
                {
                    if (tempDomainToLoadAssembly != null)
                    {
                        AppDomain.Unload(tempDomainToLoadAssembly);
                        long endTimeInTicks = Stopwatch.GetTimestamp();
                        long stopWatchTicksDiff = endTimeInTicks - beginTimeInTicks;
                        double durationInMillisecs = stopWatchTicksDiff * 1000 / (double)Stopwatch.Frequency;                        
                        WindowsServerEventSource.Log.TroubleshootingMessageEvent(tempDomainName + " AppDomain  Unloaded.");
                        WindowsServerEventSource.Log.TroubleshootingMessageEvent("TimeTaken for initialization in msec:" + durationInMillisecs);
                    }                    
                }
                catch (Exception ex)
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
}
