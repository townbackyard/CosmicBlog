using Microsoft.AspNetCore.Identity;

namespace BlogWebApp.Services
{
    /// <summary>
    /// Replaces Identity's default <see cref="UpperInvariantLookupNormalizer"/>.
    /// Both produce equivalent case-insensitive lookup keys; this one stores the
    /// normalized form in lowercase, which matches the rest of the project's
    /// lowercase-id convention (the <c>Subscribers</c> container also uses
    /// lowercased email as the partition key).
    ///
    /// <para>Functionally identical to the default — affects only the contents
    /// of <c>usernameNormalized</c> and <c>emailNormalized</c> on user docs.
    /// Pretty <c>username</c> / <c>email</c> fields keep the original case.</para>
    /// </summary>
    public class LowerInvariantLookupNormalizer : ILookupNormalizer
    {
        public string? NormalizeName(string? name) =>
            name?.Normalize().ToLowerInvariant();

        public string? NormalizeEmail(string? email) =>
            email?.Normalize().ToLowerInvariant();
    }
}
