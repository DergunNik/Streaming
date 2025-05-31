namespace DataSerializationUtils.Tests;

public class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public Person? Resident { get; set; }
}

public class Person
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public bool IsStudent { get; set; }
    public DateTime BirthDate { get; set; }
    public List<string>? Hobbies { get; set; }
    public Address? Address { get; set; }
} 