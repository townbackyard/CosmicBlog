using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace BlogWebApp.Models
{
    public class CosmicBlogUser
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get { return UserId; } }

        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty(PropertyName = "username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "usernameNormalized")]
        public string UsernameNormalized { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "emailNormalized")]
        public string EmailNormalized { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "securityStamp")]
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty(PropertyName = "roles")]
        public List<string> Roles { get; set; } = new();

        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    }
}
