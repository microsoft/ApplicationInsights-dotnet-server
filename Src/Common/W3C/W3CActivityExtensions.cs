﻿namespace Microsoft.ApplicationInsights.W3C
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.ApplicationInsights.Common;

    /// <summary>
    /// Extends Activity to support W3C distributed tracing standard.
    /// </summary>
    [Obsolete("Not ready for public consumption.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
#if DEPENDENCY_COLLECTOR
    public
#else
    internal
#endif
    static class W3CActivityExtensions
    {
        /// <summary>
        /// Generate new W3C context.
        /// </summary>
        /// <param name="activity">Activity to generate W3C context on.</param>
        /// <returns>The same Activity for chaining.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Activity GenerateW3CContext(this Activity activity)
        {
            activity.SetVersion(W3CConstants.DefaultVersion);
            activity.SetSampled(W3CConstants.DefaultSampled);
            activity.SetSpanId(StringUtilities.GenerateSpanId());
            activity.SetTraceId(StringUtilities.GenerateTraceId());
            return activity;
        }

        /// <summary>
        /// Checks if current Actuvuty has W3C properties on it.
        /// </summary>
        /// <param name="activity">Activity to check.</param>
        /// <returns>True if Activity has W3C properties, false otherwise.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool IsW3CActivity(this Activity activity)
        {
            return activity != null && activity.Tags.Any(t => t.Key == W3CConstants.TraceIdTag);
        }

        /// <summary>
        /// Updates context on the Activity based on the W3C Context in the parent Activity tree.
        /// </summary>
        /// <param name="activity">Activity to update W3C context on.</param>
        /// <returns>The same Activity for chaining.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Activity UpdateContextOnActivity(this Activity activity)
        {
            if (activity == null || activity.Tags.Any(t => t.Key == W3CConstants.TraceIdTag))
            {
                return activity;
            }

            // no w3c Tags on Activity
            activity.Parent.UpdateContextOnActivity();

            // at this point, Parent has W3C tags, but current activity does not - update it
            return activity.UpdateContextFromParent();
        }

        /// <summary>
        /// Gets traceparent header value for the Activity or null if there is no W3C context on it.
        /// </summary>
        /// <param name="activity">Activity to read W3C context from.</param>
        /// <returns>traceparent header value.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetTraceParent(this Activity activity)
        {
            string version = null, traceId = null, spanId = null, sampled = null;
            foreach (var tag in activity.Tags)
            {
                switch (tag.Key)
                {
                    case W3CConstants.TraceIdTag:
                        traceId = tag.Value;
                        break;
                    case W3CConstants.SpanIdTag:
                        spanId = tag.Value;
                        break;
                    case W3CConstants.VersionTag:
                        version = tag.Value;
                        break;
                    case W3CConstants.SampledTag:
                        sampled = tag.Value;
                        break;
                }
            }

            if (traceId == null || spanId == null || version == null || sampled == null)
            {
                return null;
            }

            return string.Join("-", version, traceId, spanId, sampled);
        }

        /// <summary>
        /// Initializes W3C context on the Activity from traceparent header value.
        /// </summary>
        /// <param name="activity">Activity to set W3C context on.</param>
        /// <param name="value">Valid traceparent header like 00-0af7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01.</param>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetTraceparent(this Activity activity, string value)
        {
            if (value != null)
            {
                var parts = value.Trim(' ', '-').Split('-');
                if (parts.Length == 4)
                {
                    string traceId = parts[1];
                    string sampled = parts[3];
                    string parentSpanId = parts[2];

                    if (traceId.Length == 32 && parentSpanId.Length == 16)
                    {
                        // we only support 00 version and ignore caller version
                        activity.SetVersion(W3CConstants.DefaultVersion);
                        
                        // we always defer sampling
                        switch (sampled)
                        {
                            case "00":
                                activity.SetSampled("02");
                                break;
                            case "01":
                                activity.SetSampled("03");
                                break;
                            case "02":
                                activity.SetSampled("02");
                                break;
                            case "03":
                                activity.SetSampled("03");
                                break;
                            default:
                                activity.SetSampled(W3CConstants.DefaultTraceFlags);
                        }

                        activity.SetParentSpanId(parentSpanId);
                        activity.SetSpanId(StringUtilities.GenerateSpanId());
                        activity.SetTraceId(traceId);
                    }
                }
            }
        }

        /// <summary>
        /// Gets tracestate header value from the Activity.
        /// </summary>
        /// <param name="activity">Activity to get tracestate from.</param>
        /// <returns>tracestate header value.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetTracestate(this Activity activity) =>
            activity.Tags.FirstOrDefault(t => t.Key == W3CConstants.TraceStateTag).Value;

        /// <summary>
        /// Sets tracestate header value on the Activity.
        /// </summary>
        /// <param name="activity">Activity to set tracestate on.</param>
        /// <param name="value">tracestate header value.</param>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetTraceState(this Activity activity, string value) =>
            activity.AddTag(W3CConstants.TraceStateTag, value);

        /// <summary>
        /// Gets TraceId from the Activity.
        /// Use carefully: if may cause iteration over all tags!
        /// </summary>
        /// <param name="activity">Activity to get traceId from.</param>
        /// <returns>TraceId value or null if it does not exist.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetTraceId(this Activity activity) => activity.Tags.FirstOrDefault(t => t.Key == W3CConstants.TraceIdTag).Value;

        /// <summary>
        /// Gets SpanId from the Activity.
        /// Use carefully: if may cause iteration over all tags!
        /// </summary>
        /// <param name="activity">Activity to get spanId from.</param>
        /// <returns>SpanId value or null if it does not exist.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetSpanId(this Activity activity) => activity.Tags.FirstOrDefault(t => t.Key == W3CConstants.SpanIdTag).Value;

        /// <summary>
        /// Gets ParentSpanId from the Activity.
        /// Use carefully: if may cause iteration over all tags!
        /// </summary>
        /// <param name="activity">Activity to get ParentSpanId from.</param>
        /// <returns>ParentSpanId value or null if it does not exist.</returns>
        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string GetParentSpanId(this Activity activity) => activity.Tags.FirstOrDefault(t => t.Key == W3CConstants.ParentSpanIdTag).Value;

        [Obsolete("Not ready for public consumption.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static void SetParentSpanId(this Activity activity, string value) =>
            activity.AddTag(W3CConstants.ParentSpanIdTag, value);

        private static void SetTraceId(this Activity activity, string value) =>
            activity.AddTag(W3CConstants.TraceIdTag, value);

        private static void SetSpanId(this Activity activity, string value) =>
            activity.AddTag(W3CConstants.SpanIdTag, value);

        private static void SetVersion(this Activity activity, string value) =>
            activity.AddTag(W3CConstants.VersionTag, value);

        private static void SetSampled(this Activity activity, string value) =>
            activity.AddTag(W3CConstants.SampledTag, value);

        private static Activity UpdateContextFromParent(this Activity activity)
        {
            if (activity != null && activity.Tags.All(t => t.Key != W3CConstants.TraceIdTag))
            {
                if (activity.Parent == null)
                {
                    activity.GenerateW3CContext();
                }
                else
                {
                    foreach (var tag in activity.Parent.Tags)
                    {
                        switch (tag.Key)
                        {
                            case W3CConstants.TraceIdTag:
                                activity.SetTraceId(tag.Value);
                                break;
                            case W3CConstants.SpanIdTag:
                                activity.SetParentSpanId(tag.Value);
                                activity.SetSpanId(StringUtilities.GenerateSpanId());
                                break;
                            case W3CConstants.VersionTag:
                                activity.SetVersion(tag.Value);
                                break;
                            case W3CConstants.SampledTag:
                                activity.SetSampled(tag.Value);
                                break;
                            case W3CConstants.TraceStateTag:
                                activity.SetTraceState(tag.Value);
                                break;
                        }
                    }
                }
            }

            return activity;
        }
    }
}
