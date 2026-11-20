using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Platform.Identity;

namespace Flatly.Api.Modules.Chores.Catalogue;

/// <summary>
/// A predefined chore a flat can add (#90): its key, its default recurrence and weight, and its name in each language.
/// The key is stored on the chore the flat creates, so adding the same predefined chore twice does not duplicate it.
/// </summary>
/// <param name="Key">Stable identifier, for example <c>cleaning</c>. It never changes once released.</param>
/// <param name="RecurrenceUnit">Default recurrence unit.</param>
/// <param name="RecurrenceEvery">Default recurrence interval.</param>
/// <param name="Weight">Default weight, from 1 to 5.</param>
/// <param name="NameEs">Name in Spanish.</param>
/// <param name="NameEn">Name in English.</param>
public sealed record PredefinedChore(
    string Key,
    RecurrenceUnit RecurrenceUnit,
    int RecurrenceEvery,
    int Weight,
    string NameEs,
    string NameEn)
{
    /// <summary>The name in <paramref name="language"/>: English for <c>en</c>, Spanish otherwise.</summary>
    public string NameFor(string language) => language == UserLanguages.English ? NameEn : NameEs;
}

/// <summary>The predefined chores of the catalogue (#17): cleaning, trash, bathroom, kitchen, shopping, recycling, bed sheets and basic maintenance.</summary>
public static class PredefinedChores
{
    /// <summary>Every predefined chore, in the order the catalogue shows them.</summary>
    public static readonly IReadOnlyList<PredefinedChore> All =
    [
        new("cleaning", RecurrenceUnit.Week, 1, 3, "Limpieza general", "General cleaning"),
        new("trash", RecurrenceUnit.Day, 2, 1, "Sacar la basura", "Take out the trash"),
        new("bathroom", RecurrenceUnit.Week, 1, 3, "Limpiar el baño", "Clean the bathroom"),
        new("kitchen", RecurrenceUnit.Week, 1, 2, "Limpiar la cocina", "Clean the kitchen"),
        new("shopping", RecurrenceUnit.Week, 1, 2, "Hacer la compra", "Grocery shopping"),
        new("recycling", RecurrenceUnit.Week, 1, 1, "Reciclaje", "Recycling"),
        new("bed_sheets", RecurrenceUnit.Week, 2, 2, "Cambiar las sábanas", "Change the bed sheets"),
        new("basic_maintenance", RecurrenceUnit.Month, 1, 3, "Mantenimiento básico", "Basic maintenance"),
    ];

    /// <summary>The predefined chore with this key, or null when the key is unknown.</summary>
    public static PredefinedChore? Find(string key) => All.FirstOrDefault(chore => chore.Key == key);
}
