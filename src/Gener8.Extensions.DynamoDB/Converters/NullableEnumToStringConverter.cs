using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;

namespace Gener8.Converters;

public class NullableEnumToStringConverter<TEnum> : IPropertyConverter where TEnum : struct, Enum
{
    public object? FromEntry(DynamoDBEntry? entry)
    {
        var s = entry?.AsString();
        if (string.IsNullOrEmpty(s))
            return null;

        return (TEnum)Enum.Parse(typeof(TEnum), s);
    }

    public DynamoDBEntry ToEntry(object? value)
    {
        if (value is TEnum e)
            return new Primitive(e.ToString());

        return new DynamoDBNull();
    }
}
