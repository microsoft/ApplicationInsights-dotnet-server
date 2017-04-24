namespace Microsoft.ApplicationInsights.Common
{
    using System;
#if NETSTANDARD || NET45
    using System.Diagnostics.Tracing;
#endif
#if NETSTANDARD
    using System.Reflection;
#endif
    using Extensibility.Implementation.Tracing;
#if NET40
    using Microsoft.Diagnostics.Tracing;
#endif

    /// <summary>
    /// ETW EventSource tracing class.
    /// </summary>
    [EventSource(Name = "Microsoft-ApplicationInsights-Extensibility-AppMapCorrelation")]
    internal sealed class AppMapCorrelationEventSource : EventSource
    {
        public static readonly AppMapCorrelationEventSource Log = new AppMapCorrelationEventSource();

        private AppMapCorrelationEventSource()
        {
            this.ApplicationName = this.GetApplicationName();
        }

        public string ApplicationName { [NonEvent]get; [NonEvent]private set; }

        [Event(
            1,
            Keywords = Keywords.UserActionable,
            Message = "Failed to retrieve App ID for the current application insights resource. Make sure the configured instrumentation key is valid. Error: {0}",
            Level = EventLevel.Warning)]
        public void FetchAppIdFailed(string exception, string appDomainName = "Incorrect")
        {
            this.WriteEvent(1, exception, this.ApplicationName);
        }

        [Event(
            2,
            Keywords = Keywords.Diagnostics,
            Message = "Failed to add cross component correlation header. Error: {0}",
            Level = EventLevel.Warning)]
        public void SetCrossComponentCorrelationHeaderFailed(string exception, string appDomainName = "Incorrect")
        {
            this.WriteEvent(2, exception, this.ApplicationName);
        }

        [Event(
            3,
            Keywords = Keywords.Diagnostics,
            Message = "Failed to determine cross component correlation header. Error: {0}",
            Level = EventLevel.Warning)]
        public void GetCrossComponentCorrelationHeaderFailed(string exception, string appDomainName = "Incorrect")
        {
            this.WriteEvent(3, exception, this.ApplicationName);
        }

        [Event(
            4,
            Keywords = Keywords.Diagnostics,
            Message = "Failed to determine role name header. Error: {0}",
            Level = EventLevel.Warning)]
        public void GetComponentRoleNameHeaderFailed(string exception, string appDomainName = "Incorrect")
        {
            this.WriteEvent(3, exception, this.ApplicationName);
        }

        [Event(
            5,
            Keywords = Keywords.Diagnostics,
            Message = "Unknown error occurred.",
            Level = EventLevel.Warning)]
        public void UnknownError(string exception, string appDomainName = "Incorrect")
        {
            this.WriteEvent(4, exception, this.ApplicationName);
        }

        [NonEvent]
        private string GetApplicationName()
        {
            string name;
            try
            {
#if NETSTANDARD1_3
                // Assembly.GetEntryAssembly() is not available on netstandard1.3, but is available in runtime
                var getEntryAssemblyMethod =
                    typeof(Assembly).GetMethod("GetEntryAssembly", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                var assembly = getEntryAssemblyMethod.Invoke(obj: null, parameters: Array.Empty<object>()) as Assembly;
                name = assembly.FullName;
#elif NETSTANDARD1_5
                name = Assembly.GetEntryAssembly().FullName;
#else // not NETSTANDARD
                name = AppDomain.CurrentDomain.FriendlyName;
#endif
            }
            catch (Exception exp)
            {
                name = "Undefined " + exp;
            }

            return name;
        }

        /// <summary>
        /// Keywords for the <see cref="AppMapCorrelationEventSource"/>.
        /// </summary>
        public sealed class Keywords
        {
            /// <summary>
            /// Key word for user actionable events.
            /// </summary>
            public const EventKeywords UserActionable = (EventKeywords)0x1;

            /// <summary>
            /// Key word for diagnostics events.
            /// </summary>
            public const EventKeywords Diagnostics = (EventKeywords)0x2;
        }
    }
}