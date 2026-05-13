using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlogWebApp
{
    public class AppSettings
    {

        public string BlogName { get; set; } = "CosmicBlog";

        public ContactSettings Contact { get; set; } = new();

    }

    public class ContactSettings
    {
        public string AcsConnectionString { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;

        // Case-insensitive keyword list -- messages whose body contains any
        // of these are silently dropped. The TownBackyard reference hardcodes
        // "CHANEL"; we keep that as a default but make it overridable for
        // self-hosters.
        public string[] SpamKeywords { get; set; } = new[] { "CHANEL" };
    }
}
