using System;

namespace TaskTrackingSystem.Shared.Models.Attachment
{
    public class FileAttachmentDto
    {
        public long Id { get; set; }
        public long TaskId { get; set; }
        public string FileName { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public long FileSizeInBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public string CreatedByName { get; set; } = null!;
    }
}
