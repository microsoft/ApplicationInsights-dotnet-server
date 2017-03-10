namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.Helpers;

    /// <summary>
    /// Document stream defines a stream of full telemetry documents that need to be collected and reported.
    /// </summary>
    internal class DocumentStream
    {
        private const float MaxTelemetryQuota = 30f;

        private const float InitialTelemetryQuota = 3f;

        private readonly DocumentStreamInfo info;

        private readonly List<FilterConjunctionGroup<RequestTelemetry>> requestFilterGroups = new List<FilterConjunctionGroup<RequestTelemetry>>();

        private readonly List<FilterConjunctionGroup<DependencyTelemetry>> dependencyFilterGroups = new List<FilterConjunctionGroup<DependencyTelemetry>>();

        private readonly List<FilterConjunctionGroup<ExceptionTelemetry>> exceptionFilterGroups = new List<FilterConjunctionGroup<ExceptionTelemetry>>();

        private readonly List<FilterConjunctionGroup<EventTelemetry>> eventFilterGroups = new List<FilterConjunctionGroup<EventTelemetry>>();

        public QuickPulseQuotaTracker RequestQuotaTracker { get; }

        public QuickPulseQuotaTracker DependencyQuotaTracker { get; }

        public QuickPulseQuotaTracker ExceptionQuotaTracker { get; }

        public QuickPulseQuotaTracker EventQuotaTracker { get; }

        public string Id => this.info.Id;

        public DocumentStream(
            DocumentStreamInfo info,
            out string[] errors,
            Clock timeProvider,
            float? initialRequestQuota = null,
            float? initialDependencyQuota = null,
            float? initialExceptionQuota = null,
            float? initialEventQuota = null)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            this.CreateFilters(out errors);

            this.RequestQuotaTracker = new QuickPulseQuotaTracker(
                timeProvider,
                MaxTelemetryQuota,
                initialRequestQuota ?? InitialTelemetryQuota);

            this.DependencyQuotaTracker = new QuickPulseQuotaTracker(
                timeProvider,
                MaxTelemetryQuota,
                initialDependencyQuota ?? InitialTelemetryQuota);

            this.ExceptionQuotaTracker = new QuickPulseQuotaTracker(
                timeProvider,
                MaxTelemetryQuota,
                initialExceptionQuota ?? InitialTelemetryQuota);

            this.EventQuotaTracker = new QuickPulseQuotaTracker(
                timeProvider,
                MaxTelemetryQuota,
                initialEventQuota ?? InitialTelemetryQuota);
        }

        public bool CheckFilters(RequestTelemetry document, out string[] errors)
        {
            return DocumentStream.CheckFilters(
                this.requestFilterGroups,
                (filterGroup, errorList) =>
                    {
                        string[] groupErrors;
                        bool groupPassed = filterGroup.CheckFilters(document, out groupErrors);
                        errorList.AddRange(groupErrors ?? new string[0]);

                        return groupPassed;
                    },
                out errors);
        }

        public bool CheckFilters(DependencyTelemetry document, out string[] errors)
        {
            return DocumentStream.CheckFilters(
                this.dependencyFilterGroups,
                (filterGroup, errorList) =>
                    {
                        string[] groupErrors;
                        bool groupPassed = filterGroup.CheckFilters(document, out groupErrors);
                        errorList.AddRange(groupErrors ?? new string[0]);

                        return groupPassed;
                    },
                out errors);
        }

        public bool CheckFilters(ExceptionTelemetry document, out string[] errors)
        {
            return DocumentStream.CheckFilters(
                this.exceptionFilterGroups,
                (filterGroup, errorList) =>
                    {
                        string[] groupErrors;
                        bool groupPassed = filterGroup.CheckFilters(document, out groupErrors);
                        errorList.AddRange(groupErrors ?? new string[0]);

                        return groupPassed;
                    },
                out errors);
        }

        public bool CheckFilters(EventTelemetry document, out string[] errors)
        {
            return DocumentStream.CheckFilters(
                this.eventFilterGroups,
                (filterGroup, errorList) =>
                    {
                        string[] groupErrors;
                        bool groupPassed = filterGroup.CheckFilters(document, out groupErrors);
                        errorList.AddRange(groupErrors ?? new string[0]);

                        return groupPassed;
                    },
                out errors);
        }

        private static bool CheckFilters<TTelemetry>(List<FilterConjunctionGroup<TTelemetry>> filterGroups, Func<FilterConjunctionGroup<TTelemetry>, List<string>, bool> checkFilters, out string[] errors)
        {
            errors = new string[0];
            var errorList = new List<string>();
            bool atLeastOneConjunctionGroupPassed = false;

            if (filterGroups.Count == 0)
            {
                // no filters for the telemetry type - filter out, we're not interested
                return false;
            }

            // iterate over AND-connected filter groups (groups are connected via OR)
            foreach (FilterConjunctionGroup<TTelemetry> conjunctionFilterGroup in filterGroups)
            {
                bool conjunctionGroupPassed;
                try
                {
                    conjunctionGroupPassed = checkFilters(conjunctionFilterGroup, errorList);
                }
                catch (Exception e)
                {
                    // the filters have failed to run (possibly incompatible field value in telemetry), consider the telemetry item filtered out by this conjunction group
                    errorList.Add(e.ToString());
                    conjunctionGroupPassed = false;
                }

                if (conjunctionGroupPassed)
                {
                    // no need to check remaining groups, one OR-connected group has passed
                    atLeastOneConjunctionGroupPassed = true;
                    break;
                }
            }

            errors = errorList.ToArray();

            return atLeastOneConjunctionGroupPassed;
        }
        
        private void CreateFilters(out string[] errors)
        {
            var errorList = new List<string>();
            foreach (DocumentFilterConjunctionGroupInfo documentFilterConjunctionGroupInfo in this.info.DocumentFilterGroups ?? new DocumentFilterConjunctionGroupInfo[0])
            {
                try
                {
                    string[] groupErrors;
                    switch (documentFilterConjunctionGroupInfo.TelemetryType)
                    {
                        case TelemetryType.Request:
                            this.requestFilterGroups.Add(new FilterConjunctionGroup<RequestTelemetry>(documentFilterConjunctionGroupInfo.Filters, out groupErrors));
                            break;
                        case TelemetryType.Dependency:
                            this.dependencyFilterGroups.Add(new FilterConjunctionGroup<DependencyTelemetry>(documentFilterConjunctionGroupInfo.Filters, out groupErrors));
                            break;
                        case TelemetryType.Exception:
                            this.exceptionFilterGroups.Add(new FilterConjunctionGroup<ExceptionTelemetry>(documentFilterConjunctionGroupInfo.Filters, out groupErrors));
                            break;
                        case TelemetryType.Event:
                            this.eventFilterGroups.Add(new FilterConjunctionGroup<EventTelemetry>(documentFilterConjunctionGroupInfo.Filters, out groupErrors));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Unsupported TelemetryType: '{0}'", documentFilterConjunctionGroupInfo.TelemetryType));
                    }

                    errorList.AddRange(groupErrors);
                }
                catch (Exception e)
                {
                    errorList.Add(string.Format(CultureInfo.InvariantCulture, "Failed to create a filter {0}. Error message: {1}", documentFilterConjunctionGroupInfo.ToString(), e.ToString()));
                }
            }

            errors = errorList.ToArray();
        }
    }
}