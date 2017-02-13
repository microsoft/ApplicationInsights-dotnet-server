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
        private static readonly MethodInfo StringToStringMethodInfo = GetMethodInfo<double, string>(x => x.ToString(CultureInfo.InvariantCulture));

        private static readonly MethodInfo StringIndexOfMethodInfo = GetMethodInfo<string, string, int>((x, y) => x.IndexOf(y, StringComparison.OrdinalIgnoreCase));

        private static readonly MethodInfo StringEqualsMethodInfo = GetMethodInfo<string, string, bool>((x, y) => x.Equals(y, StringComparison.OrdinalIgnoreCase));

        private readonly Func<TTelemetry, bool> filterLambda;
        
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
            
            Expression comparisonExpression;

            try
            {
                MemberExpression fieldExpression = Expression.Property(documentExpression, filterInfo.FieldName);

                comparisonExpression = this.ProduceComparator(fieldExpression);
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not construct the filter."), e);
            }

            try
            {
                Expression<Func<TTelemetry, bool>> lambdaExpression = Expression.Lambda<Func<TTelemetry, bool>>(
                    comparisonExpression,
                    documentExpression);

                this.filterLambda = lambdaExpression.Compile();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not compile the filter."), e);
            }
        }

        public bool Check(TTelemetry document)
        {
            try
            {
                return this.filterLambda(document);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Runtime error while filtering."), e);
            }
        }

        private static MethodInfo GetMethodInfo<T, TResult>(Expression<Func<T, TResult>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
            {
                return member.Method;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Expression is not a method"), nameof(expression));
        }

        private static MethodInfo GetMethodInfo<T1, T2, TResult>(Expression<Func<T1, T2, TResult>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
            {
                return member.Method;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Expression is not a method"), nameof(expression));
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
        
        private Expression ProduceComparator(Expression fieldExpression)
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
                                return Expression.Equal(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.NotEqual:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                return Expression.NotEqual(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.LessThan:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                return Expression.LessThan(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.GreaterThan:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                return Expression.GreaterThan(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.LessThanOrEqual:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                return Expression.LessThanOrEqual(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.GreaterThanOrEqual:
                                this.ThrowOnInvalidFilter(!this.comparandDouble.HasValue);
                                return Expression.GreaterThanOrEqual(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.Contains:
                                // fieldValue.ToString(CultureInfo.InvariantCulture).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1
                                Expression toStringCall = Expression.Call(fieldConvertedExpression, StringToStringMethodInfo, Expression.Constant(CultureInfo.InvariantCulture));
                                Expression indexOfCall = Expression.Call(toStringCall, StringIndexOfMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));
                                return Expression.NotEqual(indexOfCall, Expression.Constant(-1));
                            case Predicate.DoesNotContain:
                                // fieldValue.ToString(CultureInfo.InvariantCulture).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1
                                toStringCall = Expression.Call(fieldConvertedExpression, StringToStringMethodInfo, Expression.Constant(CultureInfo.InvariantCulture));
                                indexOfCall = Expression.Call(toStringCall, StringIndexOfMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));
                                return Expression.Equal(indexOfCall, Expression.Constant(-1));
                            default:
                                this.ThrowOnInvalidFilter();
                                break;
                        }
                    }
                    break;
                case TypeCode.String:
                    {
                        Expression fieldValueOrEmptyString = Expression.Condition(
                                    Expression.Equal(fieldExpression, Expression.Constant(null)),
                                    Expression.Constant(string.Empty),
                                    fieldExpression);

                        Expression indexOfCall = Expression.Call(
                            fieldValueOrEmptyString,
                            StringIndexOfMethodInfo,
                            Expression.Constant(this.comparand),
                            Expression.Constant(StringComparison.OrdinalIgnoreCase));

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                // (fieldValue ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase)
                                return Expression.Call(
                                    fieldValueOrEmptyString,
                                    StringEqualsMethodInfo,
                                    Expression.Constant(this.comparand),
                                    Expression.Constant(StringComparison.OrdinalIgnoreCase));
                            case Predicate.NotEqual:
                                // !(fieldValue ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase)
                                return
                                    Expression.Not(
                                        Expression.Call(
                                            fieldValueOrEmptyString,
                                            StringEqualsMethodInfo,
                                            Expression.Constant(this.comparand),
                                            Expression.Constant(StringComparison.OrdinalIgnoreCase)));
                            case Predicate.Contains:
                                // fieldValue => (fieldValue ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1;
                                return Expression.NotEqual(indexOfCall, Expression.Constant(-1));
                            case Predicate.DoesNotContain:
                                // fieldValue => (fieldValue ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1;
                                return Expression.Equal(indexOfCall, Expression.Constant(-1));
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
