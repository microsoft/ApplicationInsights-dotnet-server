namespace Microsoft.ApplicationInsights.Common
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Keeps one active subscription to specific DiagnosticSource per process.
    /// Helps manage subsciptions in scenarios where multiple apps hosted in the same process.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if DEPENDENCY_COLLECTOR
    public
#else
    internal
#endif 
    class ActiveSubsciptionManager
    {
        private readonly object lockObject = new object();
        private readonly HashSet<object> subscriptions = new HashSet<object>();
        private object active;

        /// <summary>
        /// Adds listener and makes it active if there is no active listener
        /// </summary>
        public void Attach(object subscription)
        {
            Interlocked.CompareExchange(ref this.active, subscription, null);

            lock (this.lockObject)
            {
                this.subscriptions.Add(subscription);
            }
        }

        /// <summary>
        /// Removes listener and assigns new active listener if necessary
        /// </summary>
        public void Detach(object subscription)
        {
            lock (this.lockObject)
            {
                if (this.subscriptions.Contains(subscription))
                {
                    this.subscriptions.Remove(subscription);

                    Interlocked.CompareExchange(ref this.active, this.subscriptions.FirstOrDefault(), subscription);
                }
            }
        }

        /// <summary>
        /// Checks whether given subscriber is an active one.
        /// </summary>
        /// <param name="subscriber">Subscriber to check.</param>
        /// <returns>True is it is an active subscriber, false otherwise.</returns>
        public bool IsActive(object subscriber)
        {
            return object.ReferenceEquals(this.active, subscriber);
        }
    }
}
