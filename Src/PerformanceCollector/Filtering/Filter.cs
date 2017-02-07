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
        private readonly Func<TTelemetry, bool> lambda;

        private readonly Type fieldType;

        private readonly double? comparandDouble;

        private readonly bool? comparandBoolean;

        private readonly TimeSpan? comparandTimeSpan;

        private readonly string comparand;

        private readonly Predicate predicate;

        private readonly string fieldName;

        public Filter(FilterInfo filterInfo)
        {
            ValidateInput(filterInfo);

            this.fieldName = filterInfo.FieldName;
            this.predicate = filterInfo.Predicate;
            this.comparand = filterInfo.Comparand;

            this.fieldType = GetFieldType(filterInfo);

            double comparandDouble;
            this.comparandDouble = double.TryParse(filterInfo.Comparand, out comparandDouble) ? comparandDouble : (double?)null;
            
            bool comparandBoolean;
            this.comparandBoolean = bool.TryParse(filterInfo.Comparand, out comparandBoolean) ? comparandBoolean : (bool?)null;

            TimeSpan comparandTimeSpan;
            this.comparandTimeSpan = TimeSpan.TryParse(filterInfo.Comparand, CultureInfo.InvariantCulture, out comparandTimeSpan)
                                         ? comparandTimeSpan
                                         : (TimeSpan?)null;
            
            ParameterExpression documentExpression = Expression.Variable(typeof(TTelemetry));
            MemberExpression fieldExpression = Expression.Property(documentExpression, filterInfo.FieldName);

            MethodCallExpression comparisonExpression;

            try
            {
                comparisonExpression = this.ProduceComparator(fieldExpression);
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException("Could not construct the filter", e);
            }

            try
            {
                Expression<Func<TTelemetry, bool>> lambdaExpression = Expression.Lambda<Func<TTelemetry, bool>>(
                    comparisonExpression,
                    documentExpression);

                this.lambda = lambdaExpression.Compile();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException("Could not compile the filter", e);
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
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Runtime error in filter"), e);
            }
        }

        private static Type GetFieldType(FilterInfo filterInfo)
        {
            PropertyInfo fieldPropertyInfo;
            try
            {
                fieldPropertyInfo = typeof(TTelemetry).GetProperty(filterInfo.FieldName, BindingFlags.Instance | BindingFlags.Public);
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Error finding property {0} in the type {1}",
                        filterInfo.FieldName,
                        typeof(TTelemetry).FullName),
                    e);
            }

            if (fieldPropertyInfo == null)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(filterInfo),
                    string.Format(CultureInfo.InvariantCulture, "Could not find the property {0} in the type {1}", filterInfo.FieldName, typeof(TTelemetry).FullName));
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

        private MethodCallExpression ProduceComparator(Expression fieldExpression)
        {
            // this must determine an appropriate runtime comparison given the field type, the predicate, and the comparand
            TypeCode fieldTypeCode = Type.GetTypeCode(this.fieldType);
            switch (fieldTypeCode)
            {
                case TypeCode.Boolean:
                    {
                        this.ThrowOnInvalidFilter(!this.comparandBoolean.HasValue);

                        switch (this.predicate)
                        {
                            case Predicate.Equal:        
                                Func<bool, bool> comparator = fieldValue => fieldValue == this.comparandBoolean.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.NotEqual:
                                comparator = fieldValue => fieldValue != this.comparandBoolean.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            default:
                                this.ThrowOnInvalidFilter();
                                break;
                        }
                    }

                    break;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                    {
                        // in order for the expression to compile, we must cast to double unless it's already double
                        // we're using double as the common lowest denominator for all numerical types
                        Expression fieldConvertedExpression = fieldTypeCode == TypeCode.Double
                                                                  ? fieldExpression
                                                                  : Expression.ConvertChecked(fieldExpression, typeof(double));

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                Func<double, bool> comparator = fieldValue => fieldValue == this.comparandDouble.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.NotEqual:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                comparator = fieldValue => fieldValue != this.comparandDouble.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.LessThan:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                comparator = fieldValue => fieldValue < this.comparandDouble.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.GreaterThan:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                comparator = fieldValue => fieldValue > this.comparandDouble.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.LessThanOrEqual:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                comparator = fieldValue => fieldValue <= this.comparandDouble.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.GreaterThanOrEqual:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                comparator = fieldValue => fieldValue >= this.comparandDouble.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.Contains:
                                comparator = fieldValue => fieldValue.ToString(CultureInfo.InvariantCulture).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            case Predicate.DoesNotContain:
                                comparator = fieldValue => fieldValue.ToString(CultureInfo.InvariantCulture).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldConvertedExpression);
                            default:
                                this.ThrowOnInvalidFilter();
                                break;
                        }
                    }

                    break;
                case TypeCode.String:
                    {
                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                Func<string, bool> comparator = fieldValue => (fieldValue ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase);
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.NotEqual:
                                comparator = fieldValue => !(fieldValue ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase);
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.Contains:
                                comparator = fieldValue => (fieldValue ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.DoesNotContain:
                                comparator = fieldValue => (fieldValue ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            default:
                                this.ThrowOnInvalidFilter();
                                break;
                        }
                    }

                    break;
                default:
                    if (this.fieldType == typeof(bool?))
                    {
                        throw new NotImplementedException();

                        // isFieldPredicateCompatible = this.predicate == Predicate.Equal || this.predicate == Predicate.NotEqual;
                    }
                    else if (this.fieldType == typeof(TimeSpan))
                    {
                        throw new NotImplementedException();

                        // isFieldPredicateCompatible = this.predicate != Predicate.Contains && this.predicate != Predicate.DoesNotContain;
                    }
                    else
                    {
                        this.ThrowOnInvalidFilter();
                    }

                    break;
            }

            return null;
        }

        private void ThrowOnInvalidFilter(bool conditionToThrow = true)
        {
            if (conditionToThrow)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The filter is invalid. Field: '{0}', field type: '{1}', predicate: '{2}', comparand: '{3}', document type: '{4}'",
                        this.fieldName,
                        this.fieldType.FullName,
                        this.predicate,
                        this.comparand,
                        typeof(TTelemetry).FullName));
            }
        }
    }
}
