using Markdig;
using Microsoft.AspNetCore.Html;

namespace BlogWebApp.Services
{
    public interface IMarkdownRenderer
    {
        /// <summary>
        /// Returns trusted HTML for the given content. Dispatches on the supplied
        /// <paramref name="format"/>: <c>"markdown"</c> → Markdig HTML render,
        /// <c>"html"</c> (or any non-markdown value) → the content is returned
        /// as-is for legacy round-trip rendering via <c>@Html.Raw</c>.
        /// </summary>
        IHtmlContent Render(string content, string format);
    }

    public class MarkdownRenderer : IMarkdownRenderer
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownRenderer()
        {
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()  // tables, task lists, autolinks, etc.
                .Build();
        }

        public IHtmlContent Render(string content, string format)
        {
            if (string.IsNullOrEmpty(content)) return HtmlString.Empty;

            return format == "markdown"
                ? new HtmlString(Markdown.ToHtml(content, _pipeline))
                : new HtmlString(content);  // legacy "html" or null/empty format
        }
    }
}
