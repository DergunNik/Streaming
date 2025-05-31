using System;
using System.Collections.Generic;
using DataSerializationUtils.Yaml;
using Xunit;

namespace DataSerializationUtils.Tests;

public class YamlSerializerTests
{
    private readonly YamlSerializer _serializer;

    public YamlSerializerTests()
    {
        _serializer = new YamlSerializer();
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

        var yaml = _serializer.Serialize(person);
        var deserializedPerson = _serializer.Deserialize<Person>(yaml);

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
        var yaml = _serializer.Serialize<object>(null);
        Assert.Equal("null", yaml);
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

        var yaml = _serializer.Serialize(list);
        var deserializedList = _serializer.Deserialize<List<string>>(yaml);

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

        var yaml = _serializer.Serialize(text);
        var deserializedText = _serializer.Deserialize<string>(yaml);

        Assert.Equal(text, deserializedText);
    }

    [Fact]
    public void Serialize_ComplexObject_ShouldProduceCorrectYamlFormat()
    {
        var person = new Person
        {
            Name = "John Doe",
            Age = 30,
            IsStudent = true,
            Hobbies = new List<string> { "reading", "gaming" }
        };

        var yaml = _serializer.Serialize(person);

        Assert.Contains("Name: John Doe", yaml);
        Assert.Contains("Age: 30", yaml);
        Assert.Contains("IsStudent: true", yaml);
        Assert.Contains("Hobbies:", yaml);
        Assert.Contains("- reading", yaml);
        Assert.Contains("- gaming", yaml);
    }

    [Fact]
    public void Serialize_CircularReference_ShouldHandleGracefully()
    {
        var person = new Person
        {
            Name = "John Doe",
            Age = 30,
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York"
            }
        };
        person.Address.Resident = person;

        var yaml = _serializer.Serialize(person);
        var deserializedPerson = _serializer.Deserialize<Person>(yaml);

        Assert.NotNull(deserializedPerson);
        Assert.Equal(person.Name, deserializedPerson.Name);
        Assert.Equal(person.Age, deserializedPerson.Age);
        Assert.NotNull(deserializedPerson.Address);
        Assert.Equal(person.Address.Street, deserializedPerson.Address.Street);
        Assert.Equal(person.Address.City, deserializedPerson.Address.City);
        Assert.Null(deserializedPerson.Address.Resident);
    }
} 