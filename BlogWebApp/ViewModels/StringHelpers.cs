using System.Text.RegularExpressions;

namespace BlogWebApp.ViewModels
{
    public static class StringHelpers
    {
        public static string Truncate(this string s, int max) =>
            string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "…");

        public static string StripHtml(this string s) =>
            string.IsNullOrEmpty(s) ? s : Regex.Replace(s, "<[^>]+>", " ").Replace("  ", " ").Trim();
    }
}
