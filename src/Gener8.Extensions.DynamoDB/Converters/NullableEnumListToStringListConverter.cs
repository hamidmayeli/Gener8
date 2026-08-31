using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gener8.Converters;

public class NullableEnumListToStringListConverter<TEnum> : IPropertyConverter where TEnum : struct, Enum
{
    public object? FromEntry(DynamoDBEntry? entry)
    {
        var dynamoList = entry as DynamoDBList;
        if (dynamoList == null) return null;

        return dynamoList.Entries
            .Select(e =>
            {
                var s = e.AsString();
                return string.IsNullOrEmpty(s) ? (TEnum?)null : (TEnum)Enum.Parse(typeof(TEnum), s);
            })
            .ToList();
    }

    public DynamoDBEntry ToEntry(object? value)
    {
        if (value is IEnumerable<TEnum?> list)
            return new DynamoDBList(list.Select<TEnum?, DynamoDBEntry>(e =>
                e.HasValue ? new Primitive(e.Value.ToString()) : new DynamoDBNull()));

        return new DynamoDBNull();
    }
}
