using System;
using System.ComponentModel;

namespace TaskTrackingSystem.Shared.Enums
{
    public enum EnumAdminAction
    {
        Approve,
        Reject,
        Create,
        Edit,
        Update,
        Delete,
        Detail,
    }

    public enum EnumAdminMenuCode
    {
        Dashboard,
        [Description("CMC001")] ProjectList,
        [Description("CMC002")] TaskList,
        [Description("CMC003")] UserList,
        [Description("CMC004")] RoleList,
        [Description("CMC005")] ReportList,
    }

    public enum EnumAdminActionCode
    {
        Approve,
        Reject,
        Create,
        Edit,
        Update,
        Delete,
        Detail,
    }
}