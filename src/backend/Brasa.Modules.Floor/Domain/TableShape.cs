namespace Brasa.Modules.Floor.Domain;

/// <summary>How a table renders on the floor plan.</summary>
public enum TableShape
{
    /// <summary>Round table.</summary>
    Round = 0,

    /// <summary>Square table.</summary>
    Square = 1,

    /// <summary>Rectangular table — typically pushed together for large parties.</summary>
    Rectangle = 2,
}
