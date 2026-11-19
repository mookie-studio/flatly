namespace Flatly.Api.Data.Tenancy;

/// <summary>
/// The flat of the current request. The flat route (see <c>FlatRoute</c>) sets it after checking the caller's membership,
/// before the endpoint runs. While it is null, the query filter of every <see cref="IFlatScoped"/> entity matches no rows,
/// so a query that forgets its flat returns nothing instead of data from every flat.
/// </summary>
public sealed class CurrentFlat
{
    /// <summary>Identifier of the flat in scope, or null when the request has no flat.</summary>
    public Guid? Id { get; set; }
}
