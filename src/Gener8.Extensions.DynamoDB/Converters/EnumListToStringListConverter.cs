using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gener8.Converters;

public class EnumListToStringListConverter<TEnum> : IPropertyConverter where TEnum : struct, Enum
{
    public object? FromEntry(DynamoDBEntry? entry)
    {
        var stringList = entry?.AsListOfString();
        if (stringList == null) return null;
        return stringList.Select(s => (TEnum)Enum.Parse(typeof(TEnum), s)).ToList();
    }

    public DynamoDBEntry ToEntry(object? value)
    {
        if (value is IEnumerable<TEnum> list)
            return new DynamoDBList(list.Select(e => (DynamoDBEntry)new Primitive(e.ToString())));

        return new DynamoDBNull();
    }
}
