using System;

namespace BlogWebApp.ViewModels
{
    public interface IOgContent
    {
        string Title { get; }
        string Description { get; }
        string UrlPath { get; }   // e.g., "/posts/my-slug"
        DateTime DateCreated { get; }
    }
}
