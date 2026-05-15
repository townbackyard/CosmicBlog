using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlogWebApp.Models
{
    public class BlogPost
    {

        [JsonProperty(PropertyName = "id")]
        public string Id
        {
            get
            {
                return PostId;
            }
        }

        [JsonProperty(PropertyName = "postId")]
        public string PostId { get; set; } = string.Empty;


        /// <summary>
        /// Content type. Valid values: "post", "note", "now". The same Cosmos
        /// container holds all three so the homepage activity feed is a single
        /// cross-type query.
        /// </summary>
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; } = "post";

        [JsonProperty(PropertyName = "slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "linkUrl")]
        public string? LinkUrl { get; set; }

        [JsonProperty(PropertyName = "dateUpdated")]
        public DateTime? DateUpdated { get; set; }

        /// <summary>
        /// Lifecycle status. Valid values: "draft", "published". "Scheduled" is the
        /// derived state when Status="published" AND PublishedAtUtc > UtcNow.
        /// Public queries filter Status="published" AND PublishedAtUtc <= UtcNow.
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; } = "published";

        /// <summary>
        /// When the post becomes publicly visible. Can be in the future (scheduled).
        /// Distinct from DateCreated; for legacy posts without this field, public
        /// queries should fall back to DateCreated (handled at query construction).
        /// </summary>
        [JsonProperty(PropertyName = "publishedAtUtc")]
        public DateTime? PublishedAtUtc { get; set; }

        /// <summary>
        /// Content serialization format. Valid values: "markdown", "html". New content
        /// is "markdown"; legacy content (Phase 1c TinyMCE output, Hello-World seed)
        /// is "html". The view layer dispatches the renderer on this field.
        /// </summary>
        [JsonProperty(PropertyName = "format")]
        public string Format { get; set; } = "markdown";

        [JsonProperty(PropertyName = "tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty(PropertyName = "excerpt")]
        public string? Excerpt { get; set; }

        [JsonProperty(PropertyName = "coverImageUrl")]
        public string? CoverImageUrl { get; set; }

        [JsonProperty(PropertyName = "userId")]
        public string AuthorId { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "userUsername")]
        public string AuthorUsername { get; set; } = string.Empty;


        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; } = string.Empty;


        [JsonProperty(PropertyName = "content")]
        public string Content { get; set; } = string.Empty;


        [JsonProperty(PropertyName = "commentCount")]
        public int CommentCount { get; set; }

        [JsonProperty(PropertyName = "likeCount")]
        public int LikeCount { get; set; }


        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }


        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }


    }
}
