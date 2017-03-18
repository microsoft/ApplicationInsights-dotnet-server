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
        private static string ProjectionCount = "Count()";

        private static readonly MethodInfo DoubleParseMethodInfo = typeof(double).GetMethod(
            "Parse",
            new[] { typeof(string), typeof(IFormatProvider) });

        private static readonly MethodInfo ObjectToStringMethodInfo = typeof(object).GetMethod(
            "ToString",
            BindingFlags.Public | BindingFlags.Instance);

        private Func<TTelemetry, double> projectionLambda;

        private readonly OperationalizedMetricInfo info;

        /// <summary>
        /// OR-connected collection of AND-connected filter groups.
        /// </summary>
        private readonly List<FilterConjunctionGroup<TTelemetry>> filterGroups = new List<FilterConjunctionGroup<TTelemetry>>();

        public string Id => this.info.Id;

        public AggregationType AggregationType => this.info.Aggregation;

        public OperationalizedMetric(OperationalizedMetricInfo info, out CollectionConfigurationError[] errors)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            this.CreateFilters(out errors);

            this.CreateProjection();
        }
        
        public bool CheckFilters(TTelemetry document, out CollectionConfigurationError[] errors)
        {
            if (this.filterGroups.Count < 1)
            {
                errors = new CollectionConfigurationError[0];
                return true;
            }

            var errorList = new List<CollectionConfigurationError>(this.filterGroups.Count);

            // iterate over OR-connected groups
            foreach (FilterConjunctionGroup<TTelemetry> conjunctionFilterGroup in this.filterGroups)
            {
                CollectionConfigurationError[] groupErrors;
                bool groupPassed = conjunctionFilterGroup.CheckFilters(document, out groupErrors);

                errorList.AddRange(groupErrors);

                if (groupPassed)
                {
                    // one group has passed, we don't care about others
                    errors = errorList.ToArray();
                    return true;
                }
            }

            errors = errorList.ToArray();
            return false;
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

        private void CreateFilters(out CollectionConfigurationError[] errors)
        {
            var errorList = new List<CollectionConfigurationError>();
            foreach (FilterConjunctionGroupInfo filterConjunctionGroupInfo in this.info.FilterGroups ?? new FilterConjunctionGroupInfo[0])
            {
                CollectionConfigurationError[] groupErrors = null;
                try
                {
                    var conjunctionFilterGroup = new FilterConjunctionGroup<TTelemetry>(filterConjunctionGroupInfo, out groupErrors);
                    this.filterGroups.Add(conjunctionFilterGroup);
                }
                catch (Exception e)
                {
                    errorList.Add(
                        CollectionConfigurationError.CreateError(
                            CollectionConfigurationErrorType.MetricFailureToCreateFilterUnexpected,
                            string.Format(CultureInfo.InvariantCulture, "Failed to create a filter group {0}.", filterConjunctionGroupInfo),
                            e,
                            Tuple.Create("MetricId", this.info.Id)));
                }

                if (groupErrors != null)
                {
                    foreach (var error in groupErrors)
                    {
                        error.Data["MetricId"] = this.info.Id;
                    }

                    errorList.AddRange(groupErrors);
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
                Expression fieldExpression;

                if (string.Equals(this.info.Projection, ProjectionCount, StringComparison.OrdinalIgnoreCase))
                {
                    fieldExpression = Expression.Constant(1, typeof(int));
                }
                else
                {
                    bool isCustomDimension;
                    bool isCustomMetric;
                    Filter<TTelemetry>.GetFieldType(this.info.Projection, out isCustomDimension, out isCustomMetric);
                    fieldExpression = Filter<TTelemetry>.ProduceFieldExpression(documentExpression, this.info.Projection, isCustomDimension, isCustomMetric);
                }

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