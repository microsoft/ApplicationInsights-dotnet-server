namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Diagnostics;

#if NET40
    using System.Globalization;
    using System.Threading;
    using Microsoft.Diagnostics.Tracing;
#endif
#if NET45
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Threading;
#endif

    /// <summary>
    /// Class provides methods to post event about Web event like begin or end of the request.
    /// </summary>
    [EventSource(Name = "Microsoft-ApplicationInsights-WebEventsPublisher")]
    public sealed class WebEventsPublisher : EventSource
    {
        /// <summary>
        /// WebEventsPublisher static instance.
        /// </summary>
        private static readonly WebEventsPublisher Instance = new WebEventsPublisher();

        /// <summary>
        /// Owner of this object. Only 1 owner can use 'Write' method at a time.
        /// </summary>
        private static object owner;

        private WebEventsPublisher()
        {
        }

        /// <summary>
        /// Gets the instance of WebEventsPublisher type.
        /// </summary>
        public static WebEventsPublisher Log
        {
            [NonEvent]
            get
            {
                return Instance;
            }
        }

        /// <summary>
        /// Writes events only if passed object is a current owner of EventSource instance.
        /// </summary>
        /// <param name="self">Instance of an object that writes events.</param>
        /// <param name="id">Event id.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2233:OperationsShouldNotOverflow", MessageId = "id-1", Justification = "Value will be used only for comparisson.")]
        [NonEvent]
        public void Write(object self, int id)
        {
            if (self == null)
            {
                throw new ArgumentNullException("self");    
            }

            // If owner is null set owner to self
            Interlocked.CompareExchange(ref owner, self, null);

            if (owner == self)
            {
                switch (id)
                {
                    case 1:
                        this.OnBegin();
                        break;
                    case 2:
                        this.OnEnd();
                        break;
                    case 3:
                        this.OnError();
                        break;
                    default:
                        this.Release(self);
                        throw new ArgumentException(id.ToString(CultureInfo.InvariantCulture));
                }
            }
            else
            {
                var temp = owner;

                if (temp != null)
                {
                    Debug.WriteLine("Publisher locked by " + temp.GetType());
                    if (WebEventSource.Log.IsVerboseEnabled)
                    {
                        WebEventSource.Log.PublisherLockedByType(temp.GetType().ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Release the ownership of this object.
        /// </summary>
        /// <param name="self">Object that wants to release an ownership.</param>
        [NonEvent]
        public void Release(object self)
        {
            if (self == null)
            {
                throw new ArgumentNullException("self");
            }

            // If self is an owner, set owner to null
            Interlocked.CompareExchange(ref owner, null, self);
        }

        /// <summary>
        /// Method generates event about begin of the request.
        /// </summary>
        [Event(1, Level = EventLevel.LogAlways)]
        public void OnBegin()
        {
            this.WriteEvent(1);
        }

        /// <summary>
        /// Method generates event about end of the request.
        /// </summary>
        [Event(2, Level = EventLevel.LogAlways)]
        public void OnEnd()
        {
            this.WriteEvent(2);
        }

        /// <summary>
        /// Method generates event in case if request failed.
        /// </summary>
        [Event(3, Level = EventLevel.LogAlways)]
        public void OnError()
        {
            this.WriteEvent(3);
        }
    }
}
