using System.Text.Json;

namespace TaskTrackingSystem.WebApp.Components.Partial
{
    public static class Serialization
    {
        public static readonly JsonSerializerOptions CaseInsensitive = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static readonly JsonSerializerOptions PaginationOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
