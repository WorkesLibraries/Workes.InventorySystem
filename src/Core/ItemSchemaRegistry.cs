using System;
using System.Collections.Generic;
using System.Linq;

namespace Workes.InventorySystem.Core;

public sealed class ItemSchemaRegistry<TKey>
{
    private readonly Dictionary<string, ItemSchema<TKey>> _schemas = new(StringComparer.Ordinal);

    public IEnumerable<ItemSchema<TKey>> Schemas => _schemas.Values;

    internal void Register(ItemSchema<TKey> schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        if (_schemas.TryGetValue(schema.Id, out var existing))
        {
            if (!ReferenceEquals(existing, schema))
                throw new InvalidOperationException($"Duplicate schema id '{schema.Id}' registered with a different schema instance.");
            return;
        }

        _schemas.Add(schema.Id, schema);
    }

    internal void RegisterChain(ItemSchema<TKey> schema)
    {
        var visited = new HashSet<ItemSchema<TKey>>();
        var current = schema;

        while (current != null)
        {
            if (!visited.Add(current))
                throw new InvalidOperationException($"Schema inheritance cycle detected at '{current.Id}'.");

            Register(current);
            current = current.Parent;
        }
    }

    public bool Contains(string schemaId)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
            return false;

        return _schemas.ContainsKey(schemaId);
    }

    internal void Validate()
    {
        foreach (var schema in _schemas.Values)
            ValidateParentChainKnown(schema);

        foreach (var schema in _schemas.Values)
            ValidateNoCycles(schema);

        foreach (var schema in _schemas.Values)
            ValidateNoInheritedRedefinitions(schema);
    }

    internal void Freeze()
    {
        Validate();
        foreach (var schema in _schemas.Values)
            schema.Freeze();
    }

    private void ValidateParentChainKnown(ItemSchema<TKey> schema)
    {
        var current = schema.Parent;
        while (current != null)
        {
            if (!_schemas.TryGetValue(current.Id, out var registered) || !ReferenceEquals(registered, current))
                throw new InvalidOperationException($"Schema '{schema.Id}' references unregistered parent schema '{current.Id}'.");

            current = current.Parent;
        }
    }

    private static void ValidateNoCycles(ItemSchema<TKey> schema)
    {
        var visited = new HashSet<ItemSchema<TKey>>();
        var current = schema;

        while (current != null)
        {
            if (!visited.Add(current))
                throw new InvalidOperationException($"Schema inheritance cycle detected at '{current.Id}'.");

            current = current.Parent;
        }
    }

    private static void ValidateNoInheritedRedefinitions(ItemSchema<TKey> schema)
    {
        var inheritedKeys = schema.Parent != null
            ? new HashSet<object>(schema.Parent.GetInheritedAttributeKeysForChildren())
            : new HashSet<object>();

        foreach (var directKey in schema.GetDirectAttributeKeys())
        {
            if (inheritedKeys.Contains(directKey))
                throw new InvalidOperationException($"Schema '{schema.Id}' directly redefines inherited attribute '{directKey}'.");
        }
    }
}
