namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Defines an AND group of filters.
    /// </summary>
    internal class FilterConjunctionGroup<TTelemetry>
    {
        private readonly FilterConjunctionGroupInfo info;

        private readonly List<Filter<TTelemetry>> filters = new List<Filter<TTelemetry>>();

        public FilterConjunctionGroup(FilterConjunctionGroupInfo info, out string[] errors)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            this.CreateFilters(out errors);
        }

        public bool CheckFilters(TTelemetry document, out string[] errors)
        {
            var errorList = new List<string>(this.filters.Count);

            foreach (Filter<TTelemetry> filter in this.filters)
            {
                bool filterPassed;
                try
                {
                    filterPassed = filter.Check(document);
                }
                catch (Exception e)
                {
                    // the filter has failed to run (possibly incompatible field value in telemetry), consider the telemetry item filtered out by this conjunction group
                    errorList.Add(e.ToString());
                    filterPassed = false;
                }

                if (!filterPassed)
                {
                    errors = errorList.ToArray();
                    return false;
                }
            }

            errors = errorList.ToArray();
            return true;
        }

        private void CreateFilters(out string[] errors)
        {
            var errorList = new List<string>();
            foreach (FilterInfo filterInfo in this.info.Filters)
            {
                try
                {
                    var filter = new Filter<TTelemetry>(filterInfo);

                    this.filters.Add(filter);
                }
                catch (Exception e)
                {
                    errorList.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Failed to create a filter {0}. Error message: {1}",
                            filterInfo.ToString(),
                            e.ToString()));
                }
            }

            errors = errorList.ToArray();
        }
    }
}