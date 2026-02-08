using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace CobolFixedWidthImport.Parsing.Mapping;

/// <summary>
/// Compiles and caches: ((TParent)p).Collection.Add((TChild)c)
/// for a parent collection property like "Lines" or "Fees".
/// </summary>
public sealed class CollectionAdderCache
{
    private readonly ConcurrentDictionary<(Type ParentType, string CollectionPath, Type ChildType), Action<object, object>> _cache = new();

    public Action<object, object> GetAdder(Type parentType, string collectionPath, Type childType)
        => _cache.GetOrAdd((parentType, collectionPath, childType), key => BuildAdder(key.ParentType, key.CollectionPath, key.ChildType));

    private static Action<object, object> BuildAdder(Type parentType, string collectionPath, Type childType)
    {
        var parentParam = Expression.Parameter(typeof(object), "parent");
        var childParam = Expression.Parameter(typeof(object), "child");

        var typedParent = Expression.Convert(parentParam, parentType);
        var typedChild = Expression.Convert(childParam, childType);

        // Navigate collection path (supports "Lines" or "Nested.Lines")
        Expression currentExpr = typedParent;
        Type currentType = parentType;

        var segments = collectionPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            throw new InvalidOperationException("Collection path cannot be empty.");

        PropertyInfo? collectionProp = null;
        foreach (var seg in segments)
        {
            collectionProp = currentType.GetProperty(seg, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (collectionProp is null)
                throw new InvalidOperationException($"Collection property '{seg}' not found on '{currentType.FullName}'.");

            currentExpr = Expression.Property(currentExpr, collectionProp);
            currentType = collectionProp.PropertyType;
        }

        // Find Add(T) method
        var addMethod = currentType.GetMethod("Add", new[] { childType });
        if (addMethod is null)
            throw new InvalidOperationException($"No Add({childType.Name}) method found on collection type '{currentType.FullName}' (path '{collectionPath}').");

        var callAdd = Expression.Call(currentExpr, addMethod, typedChild);
        return Expression.Lambda<Action<object, object>>(callAdd, parentParam, childParam).Compile();
    }
}
