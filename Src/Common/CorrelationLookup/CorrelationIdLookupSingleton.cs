namespace Microsoft.ApplicationInsights.Common.CorrelationLookup
{
    /// <summary>
    /// This class holds a single instance of CorrelationIdLookupHelper for the entire application.
    /// This is a workaround because we don't use dependency injection.
    /// This instance can be overwritten by UnitTests.
    /// </summary>
    internal static class CorrelationIdLookupSingleton
    {
        private static object semaphore = new object();
        private static ICorrelationIdLookupHelper instance;

        /// <summary>
        /// Gets or sets the instance of ICorrelationIdLookupHelper to be used by Modules.
        /// Modules should not copy this locally and should always refer to this Instance.
        /// Set will be used by Unit Tests.
        /// </summary>
        public static ICorrelationIdLookupHelper Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (semaphore)
                    {
                        if (instance == null)
                        {
                            instance = new CorrelationIdLookupHelper();
                        }
                    }
                }

                return instance;
            }
            
            set
            {
                lock (semaphore)
                {
                    instance = value;
                }
            }
        }
    }
}
