using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;

namespace Gener8.Converters;

public class EnumToStringConverter<TEnum> : IPropertyConverter where TEnum : struct, Enum
{
    public object FromEntry(DynamoDBEntry entry) => (TEnum)Enum.Parse(typeof(TEnum), entry.AsString());

    public DynamoDBEntry ToEntry(object value) => new Primitive(value.ToString());
}
