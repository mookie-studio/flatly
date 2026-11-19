using System.Security.Cryptography;
using Flatly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Invites;

/// <summary>
/// The invite code of a flat: a short, case-insensitive code that a new member enters to join (issue #77).
/// </summary>
public static class InviteCodes
{
    /// <summary>Number of characters in a code.</summary>
    public const int Length = 8;

    /// <summary>How long a code works after it is generated.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Characters a code may use. The look-alikes 0, O, 1 and I are left out, so a code read aloud or copied by hand
    /// is not mistaken.
    /// </summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Generates a random code with the cryptographic random generator. Does not check the database.</summary>
    public static string Generate() => new(RandomNumberGenerator.GetItems(Alphabet.AsSpan(), Length));

    /// <summary>Generates a code that no other flat holds at the moment.</summary>
    public static async Task<string> GenerateUniqueAsync(FlatlyDbContext database, CancellationToken cancellationToken)
    {
        while (true)
        {
            var code = Generate();
            var taken = await database.Flats.AnyAsync(flat => flat.InviteCode == code, cancellationToken);
            if (!taken)
            {
                return code;
            }
        }
    }

    /// <summary>Puts a code in the form it is stored and compared in: trimmed and in upper case.</summary>
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
