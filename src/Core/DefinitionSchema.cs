using System;
using System.Collections.Generic;
using System.Linq;
using Workes.InventorySystem.Attributes;
namespace Workes.InventorySystem.Core;

public class DefinitionSchema
{
    private readonly HashSet<object> _requiredKeys = new();

    public void Require<T>(AttributeKey<T> key)
    {
        _requiredKeys.Add(key);
    }

    public void Validate(AttributeContainer attributes)
    {
        foreach (var required in _requiredKeys)
        {
            if (!attributes.GetAllKeys().Contains(required))
            {
                throw new InvalidOperationException(
                    $"Missing required attribute: {required}");
            }
        }
    }
}
