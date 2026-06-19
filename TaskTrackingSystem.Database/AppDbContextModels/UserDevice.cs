using System;

namespace TaskTrackingSystem.Database.AppDbContextModels;

public partial class UserDevice
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string FcmToken { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
