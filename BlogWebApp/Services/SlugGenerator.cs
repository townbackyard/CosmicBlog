using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BlogWebApp.Services
{
    public static class SlugGenerator
    {
        public static string FromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            // Normalize unicode -> ASCII (strips diacritics)
            var normalized = title.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

            // Replace runs of non-alphanumeric with single hyphens
            var slug = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');

            // Cap length at 80 chars
            return slug.Length > 80 ? slug.Substring(0, 80).TrimEnd('-') : slug;
        }
    }
}
