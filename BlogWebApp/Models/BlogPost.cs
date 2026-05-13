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
