namespace Microsoft.ApplicationInsights.DependencyCollector.W3C
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    /// <summary>
    /// Telemetry Initializer that sets correlation ids for W3C.
    /// </summary>
    [Obsolete("Not ready for public consumption.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class W3COperationCorrelationTelemetryInitializer : ITelemetryInitializer
    {
        private static readonly string RddDiagnosticSourcePrefix = "rdd" + RddSource.DiagnosticSourceCore;

        /// <summary>
        /// Initializes telemety item.
        /// </summary>
        /// <param name="telemetry">Telemetry item.</param>
        public void Initialize(ITelemetry telemetry)
        {
            Activity currentActivity = Activity.Current;
            if (this.UpdateActivity(currentActivity))
            {
                this.UpdateTelemetry(telemetry, currentActivity);
            }
        }

        private bool UpdateActivity(Activity activity)
        {
            // if there is activity - we've gone all the way to the root and did not find any w3c tags
            // do not update telemtery
            if (activity == null)
            {
                return false;
            }

            // if activity has trace id already - return true
            if (activity.Tags.Any(t => t.Key == W3CConstants.TraceIdTag))
            {
                return true;
            }

            // no w3c Tags on Activity
            if (this.UpdateActivity(activity.Parent))
            {
                // at this point, Parent has W3C tags, but current activity does not - update it
                activity.UpdateContextFromParent();
                return true;
            }

            return false;
        }

        private void UpdateTelemetry(ITelemetry telemetry, Activity activity)
        {
            // Requests and dependnecies are initialized from the current Activity 
            // (i.e. telemetry.Id = current.Id). Activity is created for such requests specifically
            // Traces, exceptions, events on the other side are children of current activity
            // There is one exception - SQL DiagnosticSource where current Activity is a parent
            // for dependency calls.

            OperationTelemetry opTelemetry = telemetry as OperationTelemetry;
            bool initializeFromCurrent = opTelemetry != null;

            if (initializeFromCurrent)
            {
                initializeFromCurrent &= !(opTelemetry is DependencyTelemetry dependency &&
                                           dependency.Type == RemoteDependencyConstants.SQL
                                           && dependency.Context.GetInternalContext().SdkVersion
                                               .StartsWith(RddDiagnosticSourcePrefix, StringComparison.Ordinal)); 
            }

            foreach (var tag in activity.Tags)
            {
                switch (tag.Key)
                {
                    case W3CConstants.TraceIdTag:
                        telemetry.Context.Operation.Id = tag.Value;
                        break;
                    case W3CConstants.SpanIdTag:
                        if (initializeFromCurrent)
                        {
                            opTelemetry.Id = tag.Value;
}
                        else
                        {
                            telemetry.Context.Operation.ParentId = tag.Value;
                        }

                        break;
                    case W3CConstants.ParentSpanIdTag:
                        if (initializeFromCurrent)
                        {
                            telemetry.Context.Operation.ParentId = tag.Value;
                        }

                        break;
                }
            }
        }
    }
}