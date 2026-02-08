using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace CobolFixedWidthImport.Parsing.Mapping;

/// <summary>
/// Compiles and caches fast property setters for paths like "AccountNumber" or "Nested.Prop".
/// Handles nullable/value conversions safely.
/// </summary>
public sealed class PropertySetterCache
{
    private readonly ConcurrentDictionary<(Type Type, string Path), Action<object, object?>> _cache = new();

    public Action<object, object?> GetSetter(Type targetType, string propertyPath)
        => _cache.GetOrAdd((targetType, propertyPath), key => BuildSetter(key.Type, key.Path));

    private static Action<object, object?> BuildSetter(Type targetType, string propertyPath)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var typedInstance = Expression.Convert(instanceParam, targetType);

        Expression currentExpr = typedInstance;
        Type currentType = targetType;

        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            throw new InvalidOperationException("Property path cannot be empty.");

        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];

            var prop = currentType.GetProperty(seg, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null)
                throw new InvalidOperationException($"Property '{seg}' not found on type '{currentType.FullName}' (path '{propertyPath}').");

            if (i == segments.Length - 1)
            {
                if (!prop.CanWrite)
                    throw new InvalidOperationException($"Property '{prop.Name}' on '{currentType.FullName}' is not writable.");

                var propExpr = Expression.Property(currentExpr, prop);
                var convertedValueExpr = BuildConvertValueExpression(valueParam, prop.PropertyType);

                var assign = Expression.Assign(propExpr, convertedValueExpr);
                return Expression.Lambda<Action<object, object?>>(assign, instanceParam, valueParam).Compile();
            }

            if (!prop.CanRead)
                throw new InvalidOperationException($"Property '{prop.Name}' on '{currentType.FullName}' is not readable.");

            currentExpr = Expression.Property(currentExpr, prop);
            currentType = prop.PropertyType;
        }

        throw new InvalidOperationException($"Invalid property path '{propertyPath}'.");
    }

    private static Expression BuildConvertValueExpression(ParameterExpression valueParam, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        var isNullable = underlying is not null;
        var nonNullTarget = underlying ?? targetType;

        var nullConst = Expression.Constant(null, typeof(object));
        var valueIsNull = Expression.Equal(valueParam, nullConst);

        Expression nullResult = (targetType.IsValueType && !isNullable)
            ? Expression.Default(targetType)
            : Expression.Constant(null, targetType);

        // If already assignable -> cast
        var castDirect = Expression.Convert(valueParam, targetType);

        // Otherwise Convert.ChangeType(...)
        var changeTypeMethod = typeof(Convert).GetMethod(
            nameof(Convert.ChangeType),
            new[] { typeof(object), typeof(Type), typeof(IFormatProvider) })
            ?? throw new InvalidOperationException("Convert.ChangeType overload not found.");

        var converted = Expression.Call(
            changeTypeMethod,
            valueParam,
            Expression.Constant(nonNullTarget, typeof(Type)),
            Expression.Constant(CultureInfo.InvariantCulture, typeof(IFormatProvider)));

        Expression castConverted = Expression.Convert(converted, nonNullTarget);
        if (isNullable)
            castConverted = Expression.Convert(castConverted, targetType);

        var valueIsTargetType = Expression.TypeIs(valueParam, targetType);
        var whenNotNull = Expression.Condition(valueIsTargetType, castDirect, castConverted);

        return Expression.Condition(valueIsNull, nullResult, whenNotNull);
    }
}
