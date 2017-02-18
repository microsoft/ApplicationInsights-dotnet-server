namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Operationalized metric defines a configured metric that needs to be collected and reported.
    /// </summary>
    internal class OperationalizedMetric<TTelemetry>
    {
        private static readonly MethodInfo DoubleParseMethodInfo = typeof(double).GetMethod(
            "Parse",
            new[] { typeof(string), typeof(IFormatProvider) });

        private static readonly MethodInfo ObjectToStringMethodInfo = typeof(object).GetMethod(
            "ToString",
            BindingFlags.Public | BindingFlags.Instance);

        private Func<TTelemetry, double> projectionLambda;

        private readonly OperationalizedMetricInfo info;

        private readonly List<Filter<TTelemetry>> filters = new List<Filter<TTelemetry>>();

        public string Id => this.info.Id;

        public AggregationType AggregationType => this.info.Aggregation;

        public OperationalizedMetric(OperationalizedMetricInfo info, out string[] errors)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            this.CreateFilters(out errors);

            this.CreateProjection();
        }
        
        public bool CheckFilters(TTelemetry document, out string[] errors)
        {
            // AND filters
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
                    // the filter has failed to run (possibly incompatible field value in telemetry), consider the telemetry item filtered out
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

        public double Project(TTelemetry document)
        {
            try
            {
                return this.projectionLambda(document);
            }
            catch (FormatException e)
            {
                //  the projected value could not be parsed by double.Parse()
                throw new ArgumentOutOfRangeException(
                    string.Format(CultureInfo.InvariantCulture, "Projected field {0} was not a number", this.info.Projection),
                    e);
            }
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
                    throw new ArgumentOutOfRangeException(
                        nameof(aggregationType),
                        aggregationType,
                        string.Format(CultureInfo.InvariantCulture, "AggregationType is not supported"));
            }
        }

        public override string ToString()
        {
            return this.info.ToString();
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
                        string.Format(CultureInfo.InvariantCulture, "Failed to create a filter {0}. Error message: {1}", filterInfo.ToString(), e.ToString()));
                }
            }

            errors = errorList.ToArray();
        }

        private void CreateProjection()
        {
            ParameterExpression documentExpression = Expression.Variable(typeof(TTelemetry));
            
            Expression projectionExpression;

            try
            {
                MemberExpression fieldExpression = Expression.Property(documentExpression, this.info.Projection);

                // double.Parse(((object)fieldExpression).ToString());
                Expression fieldAsObjectExpression = Expression.ConvertChecked(fieldExpression, typeof(object));
                MethodCallExpression fieldExpressionToString = Expression.Call(fieldAsObjectExpression, ObjectToStringMethodInfo);
                projectionExpression = Expression.Call(DoubleParseMethodInfo, fieldExpressionToString, Expression.Constant(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not construct the projection."), e);
            }

            try
            {
                Expression<Func<TTelemetry, double>> lambdaExpression = Expression.Lambda<Func<TTelemetry, double>>(projectionExpression, documentExpression);

                this.projectionLambda = lambdaExpression.Compile();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not compile the projection."), e);
            }
        }
    }
}