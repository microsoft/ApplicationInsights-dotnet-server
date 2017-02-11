namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Operationalized metric defines a named metric that needs to be collected and reported.
    /// </summary>
    internal class OperationalizedMetric<TTelemetry>
    {
        private readonly static MethodInfo DoubleParseMethodInfo = typeof(double).GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        private readonly static MethodInfo ObjectToStringMethodInfo = typeof(object).GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance);

        private readonly Func<TTelemetry, double> projectionLambda;

        private readonly OperationalizedMetricInfo info;

        private readonly List<Filter<TTelemetry>> filters = new List<Filter<TTelemetry>>();

        /// <summary>
        /// (Id, SessionId)
        /// </summary>
        public HashSet<Tuple<string, string>> IdsToReportUnder { get; set; } = new HashSet<Tuple<string, string>>();

        public OperationalizedMetric(OperationalizedMetricInfo info, out string[] errors)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            var errorList = new List<string>();
            foreach (FilterInfo filterInfo in info.Filters)
            {
                try
                {
                    var filter = new Filter<TTelemetry>(filterInfo);

                    this.filters.Add(filter);
                }
                catch (Exception e)
                {
                    errorList.Add(e.ToString());
                }
            }

            errors = errorList.ToArray();

            // only has a single Id for now
            this.IdsToReportUnder.Add(Tuple.Create(info.Id, info.SessionId));

            // create projection expression
            ParameterExpression documentExpression = Expression.Variable(typeof(TTelemetry));
            MemberExpression fieldExpression = Expression.Property(documentExpression, info.Projection);

            Expression projectionExpression;

            try
            {
                // double.Parse(fieldExpression.ToString());
                MethodCallExpression fieldExpressionToString = Expression.Call(fieldExpression, ObjectToStringMethodInfo);
                projectionExpression = Expression.Call(DoubleParseMethodInfo, fieldExpressionToString);
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not construct the projection."), e);
            }

            try
            {
                Expression<Func<TTelemetry, double>> lambdaExpression = Expression.Lambda<Func<TTelemetry, double>>(
                    projectionExpression,
                    documentExpression);

                this.projectionLambda = lambdaExpression.Compile();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not compile the projection."), e);
            }
        }

        public bool CheckFilters(TTelemetry document, out string[] errors)
        {
            // AND filters
            var errorList = new List<string>();
            foreach (Filter<TTelemetry> filter in this.filters)
            {
                bool filterPassed;
                try
                {
                    filterPassed = filter.Check(document);
                }
                catch (Exception e)
                {
                    // the filter has failed to run, ignore it
                    errorList.Add(e.ToString());
                    continue;
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

        public double Project(TTelemetry document)
        {
            return this.projectionLambda(document);
        }

        public static double Aggregate(double[] accumulatedValue, AggregationType aggregationType)
        {
            IEnumerable<double> defaultIfEmpty = accumulatedValue.DefaultIfEmpty(0);
            switch (aggregationType)
            {
                case AggregationType.Avg:
                    return defaultIfEmpty.Average();
                case AggregationType.Sum:
                    return defaultIfEmpty.Sum();
                case AggregationType.Min:
                    return defaultIfEmpty.Min();
                case AggregationType.Max:
                    return defaultIfEmpty.Max();
                default:
                    throw new ArgumentOutOfRangeException(nameof(aggregationType), aggregationType, "AggregationType is not supported");
            }
        }

        public override string ToString()
        {
            return this.info.ToString();
        }
    }
}