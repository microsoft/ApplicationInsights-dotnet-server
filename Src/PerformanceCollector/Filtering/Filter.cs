namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Filter determines whether a telemetry document matches the criterion.
    /// </summary>
    /// <typeparam name="TTelemetry">Type of telemetry documents.</typeparam>
    internal class Filter<TTelemetry>
    {
        private readonly Func<TTelemetry, object> lambda = null;

        private readonly Type fieldType;

        private readonly double? comparandDouble = null;

        private readonly bool? comparandBoolean = null;

        private readonly TimeSpan? comparandTimeSpan = null;

        private readonly string comparand = string.Empty;
        
        //!!! refactor
        public Filter(FilterInfo filterInfo)
        {
            ValidateInput(filterInfo);

            this.fieldType = GetFieldType(filterInfo);

            double comparandDouble;
            if (double.TryParse(filterInfo.Comparand, out comparandDouble))
            {
                this.comparandDouble = comparandDouble;
            }

            bool comparandBoolean;
            if (bool.TryParse(filterInfo.Comparand, out comparandBoolean))
            {
                this.comparandBoolean = comparandBoolean;
            }

            TimeSpan comparandTimeSpan;
            if (TimeSpan.TryParse(filterInfo.Comparand, CultureInfo.InvariantCulture, out comparandTimeSpan))
            {
                this.comparandTimeSpan = comparandTimeSpan;
            }

            this.comparand = filterInfo.Comparand;

            ValidateFilterInfo(filterInfo);

            ParameterExpression parameterExpression = Expression.Variable(typeof(TTelemetry));
            MemberExpression fieldExpression = Expression.Property(parameterExpression, filterInfo.FieldName);

            switch (Type.GetTypeCode(fieldType))
            {
                case TypeCode.Boolean:
                    break;
                case TypeCode.Int32:
                case TypeCode.Double:
                    break;
                case TypeCode.String:
                    break;
                    
                default:
                    if (fieldType == typeof(bool?))
                    {
                        break;
                    }
                    else if (fieldType == typeof(TimeSpan))
                    {
                        break;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(filterInfo.FieldName),
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "The property {0} of class {2} has a type of {1}, which is not supported.",
                                filterInfo.FieldName,
                                fieldType.FullName,
                                typeof(TTelemetry).FullName));
                    }
            }

            try
            {
                
                Expression.Call()
                BinaryExpression predicateExpression;
                
                switch (filterInfo.Predicate)
                {
                    case Predicate.Equal:
                        predicateExpression = Expression.Equal()
                        break;
                    case Predicate.NotEqual:
                        break;
                    case Predicate.LessThan:
                        break;
                    case Predicate.GreaterThan:
                        break;
                    case Predicate.LessThanOrEqual:
                        break;
                    case Predicate.GreaterThanOrEqual:
                        break;
                    case Predicate.Contains:
                        break;
                    case Predicate.DoesNotContain:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Predicate is unsupported: {filterInfo.Predicate}");
                }

                Expression<Func<TTelemetry, bool>> lambdaExpression = Expression.Lambda<Func<TTelemetry, bool>>(
                    fieldExpression,
                    parameterExpression);

                this.lambda = lambdaExpression.Compile();

                //!!! call to check if it runs ok
            }
            catch (Exception e)
            {
                // couldn't create the filter

                //!!! report error?
            }
        }

        private bool CompareString(string fieldValue)
        {
            Environment.MachineName
            return fieldValue > comparandDouble;
        }

        private static Type GetFieldType(FilterInfo filterInfo)
        {
            PropertyInfo fieldPropertyInfo;
            try
            {
                fieldPropertyInfo = typeof(TTelemetry).GetProperty(filterInfo.FieldName, BindingFlags.Instance);
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Could not find the property {0} in the type {1}",
                        filterInfo.FieldName,
                        typeof(TTelemetry).FullName),
                    e);
            }

            return fieldPropertyInfo.PropertyType;
        }

        private static void ValidateInput(FilterInfo filterInfo)
        {
            if (string.IsNullOrEmpty(filterInfo.FieldName))
            {
                throw new ArgumentNullException(nameof(filterInfo.FieldName), string.Format(CultureInfo.InvariantCulture, "Parameter must be specified."));
            }

            if (filterInfo.Comparand == null)
            {
                throw new ArgumentNullException(nameof(filterInfo.Comparand), string.Format(CultureInfo.InvariantCulture, "Parameter cannot be null."));
            }
        }

        private void ValidateFilterInfo(FilterInfo filterInfo)
        {
            switch (filterInfo.Predicate)
            {
                case Predicate.Equal:
                case Predicate.NotEqual:
                case Predicate.Contains:
                case Predicate.DoesNotContain:
                    return;
                case Predicate.LessThan:
                case Predicate.GreaterThan:
                case Predicate.LessThanOrEqual:
                case Predicate.GreaterThanOrEqual:
                    if (!this.comparandDouble.HasValue && !this.comparandTimeSpan.HasValue)
                    {
                        throw new ArgumentOutOfRangeException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Invalid combination of comparand and predicate: predicate is {0}, comparand is {1}",
                                filterInfo.Predicate,
                                filterInfo.Comparand));
                    }
                    break;                
                default:
                    throw new ArgumentOutOfRangeException(
                        string.Format(CultureInfo.InvariantCulture, "Predicate is unsupported: {0}", filterInfo.Predicate));
            }

        }

        public bool Check(TTelemetry document)
        {
            try
            {
                return this.lambda(document);
            }
            catch (Exception e)
            {
                //!!! report error?
                return false;
            }
        }
    }
}
