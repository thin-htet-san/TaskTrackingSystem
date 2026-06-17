using System;

namespace TaskTrackingSystem.Shared.Models.AuditLog
{
    public class AuditLogDto
    {
        public long Id { get; set; }
        public long? UserId { get; set; }
        public string Username { get; set; } = null!;
        public string UserFullName { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string Module { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
