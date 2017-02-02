using System.Reflection;

namespace Microsoft.ApplicationInsights.WindowsServer.Implementation
{
    /// <summary>
    /// The wrapper for the Azure Service Runtime.
    /// </summary>
    internal class ServiceRuntime
    {
        /// <summary>
        /// Gets the role environment.
        /// </summary>
        /// <param name="loadedAssembly">The loaded assembly on which methods are called using reflection.</param>
        /// <returns>
        /// The role environment object.
        /// </returns>
        public RoleEnvironment GetRoleEnvironment(Assembly loadedAssembly)
        {
            // TODO: remove factory
            return new RoleEnvironment(loadedAssembly);
        }        
    }
}
