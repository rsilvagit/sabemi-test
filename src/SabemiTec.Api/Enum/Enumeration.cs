using System.Reflection;

namespace SabemiTec.Api.Enum;

public abstract class Enumeration : IComparable
{
    public int Id { get; private set; }
    public string Name { get; private set; }

    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
                 .Select(f => f.GetValue(null))
                 .Cast<T>();

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration otherValue)
        {
            return false;
        }

        var typeMatches = GetType().Equals(obj.GetType());
        var valueMatches = Id.Equals(otherValue.Id);

        return typeMatches && valueMatches;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public int CompareTo(object? obj) => Id.CompareTo(((Enumeration)obj!).Id);

    public static T FromValue<T>(int value) where T : Enumeration
    {
        var matchingItem = GetAll<T>().FirstOrDefault(e => e.Id == value);

        if (matchingItem is null)
            throw new ArgumentException($"'{value}' is not a valid value in {typeof(T)}");

        return matchingItem;
    }
}
