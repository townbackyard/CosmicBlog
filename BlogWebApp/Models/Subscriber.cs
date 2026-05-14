using Newtonsoft.Json;
using System;

namespace BlogWebApp.Models
{
    public class Subscriber
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; } = string.Empty;  // emailNormalized (lowercased)

        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "dateSubscribed")]
        public DateTime DateSubscribed { get; set; }

        [JsonProperty(PropertyName = "confirmed")]
        public bool Confirmed { get; set; } = false;
    }
}
