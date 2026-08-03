using System;

namespace TaskTrackingSystem.Shared.Models.Comment
{
    public class CommentDto
    {
        public long Id { get; set; }
        public long TaskId { get; set; }
        public long UserId { get; set; }
        public string UserFullName { get; set; } = null!;
        public string? UserFullNameMy { get; set; }
        public string UserRoleName { get; set; } = null!;
        public string? UserRoleNameMy { get; set; }
        public string Message { get; set; } = null!;
        public string? MessageMy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateCommentDto
    {
        public string Message { get; set; } = null!;
        public string? MessageMy { get; set; }
    }
}
