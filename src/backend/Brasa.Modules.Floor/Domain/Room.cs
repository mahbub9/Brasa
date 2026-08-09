using Brasa.Shared.Persistence;

namespace Brasa.Modules.Floor.Domain;

/// <summary>An area of the restaurant, e.g. "Salão" or "Esplanada".</summary>
public sealed class Room : Entity
{
    private Room()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new room.</summary>
    public Room(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Room name must not be empty.", nameof(name));
        }

        Name = name;
        DisplayOrder = displayOrder;
    }

    /// <summary>Display name, e.g. "Esplanada".</summary>
    public string Name { get; private set; }

    /// <summary>Sort position among rooms. Lower shows first.</summary>
    public int DisplayOrder { get; private set; }
}
