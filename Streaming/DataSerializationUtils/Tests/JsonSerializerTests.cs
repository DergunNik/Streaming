using System;
using System.Collections.Generic;
using DataSerializationUtils.Json;
using Xunit;

namespace DataSerializationUtils.Tests;

public class JsonSerializerTests
{
    private readonly JsonSerializer _serializer;

    public JsonSerializerTests()
    {
        _serializer = new JsonSerializer();
    }

    [Fact]
    public void SerializeDeserialize_Person_ShouldReturnEquivalentObject()
    {
        var person = new Person
        {
            Name = "John Doe",
            Age = 30,
            IsStudent = true,
            BirthDate = new DateTime(1993, 5, 15),
            Hobbies = new List<string> { "reading", "gaming", "traveling" },
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York",
                Country = "USA"
            }
        };

        var json = _serializer.Serialize(person);
        var deserializedPerson = _serializer.Deserialize<Person>(json);

        Assert.Equal(person.Name, deserializedPerson.Name);
        Assert.Equal(person.Age, deserializedPerson.Age);
        Assert.Equal(person.IsStudent, deserializedPerson.IsStudent);
        Assert.Equal(person.BirthDate, deserializedPerson.BirthDate);
        Assert.Equal(person.Hobbies.Count, deserializedPerson.Hobbies.Count);
        Assert.Equal(person.Address.Street, deserializedPerson.Address.Street);
        Assert.Equal(person.Address.City, deserializedPerson.Address.City);
        Assert.Equal(person.Address.Country, deserializedPerson.Address.Country);
    }

    [Fact]
    public void Serialize_NullObject_ShouldReturnNullString()
    {
        var json = _serializer.Serialize<object>(null);
        Assert.Equal("null", json);
    }

    [Fact]
    public void Deserialize_NullOrEmptyString_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _serializer.Deserialize<Person>(null));
        Assert.Throws<ArgumentException>(() => _serializer.Deserialize<Person>(""));
        Assert.Throws<ArgumentException>(() => _serializer.Deserialize<Person>("   "));
    }

    [Fact]
    public void SerializeDeserialize_PrimitiveTypes_ShouldReturnEquivalentValues()
    {
        int number = 42;
        bool boolean = true;
        string text = "Hello, World!";
        DateTime date = new DateTime(2024, 3, 15);

        Assert.Equal(number, _serializer.Deserialize<int>(_serializer.Serialize(number)));
        Assert.Equal(boolean, _serializer.Deserialize<bool>(_serializer.Serialize(boolean)));
        Assert.Equal(text, _serializer.Deserialize<string>(_serializer.Serialize(text)));
        Assert.Equal(date, _serializer.Deserialize<DateTime>(_serializer.Serialize(date)));
    }

    [Fact]
    public void SerializeDeserialize_ListOfStrings_ShouldReturnEquivalentList()
    {
        var list = new List<string> { "one", "two", "three" };

        var json = _serializer.Serialize(list);
        var deserializedList = _serializer.Deserialize<List<string>>(json);

        Assert.Equal(list.Count, deserializedList.Count);
        for (int i = 0; i < list.Count; i++)
        {
            Assert.Equal(list[i], deserializedList[i]);
        }
    }

    [Fact]
    public void Serialize_SpecialCharacters_ShouldEscapeCorrectly()
    {
        var text = "Line 1\nLine 2\tTabbed\r\nQuoted \"text\"";

        var json = _serializer.Serialize(text);
        var deserializedText = _serializer.Deserialize<string>(json);

        Assert.Equal(text, deserializedText);
    }
} 