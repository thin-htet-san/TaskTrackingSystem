using System;
using System.Collections.Generic;

namespace TaskTrackingSystem.Shared.Models.Report
{
    public class TimeTrackingReportDto
    {
        public decimal TotalEstimatedHrs { get; set; }
        public decimal CompletedHours { get; set; }
        public decimal Variance { get; set; }
        public double CompletionPercentage { get; set; }
        public int TasksWithHours { get; set; }
        public decimal AvgHoursPerTask { get; set; }

        public List<EmployeeTimeSummaryDto> EmployeeSummary { get; set; } = new();
        public List<ProjectTimeSummaryDto> ProjectSummary { get; set; } = new();
        public List<TaskTimeDetailDto> TaskDetail { get; set; } = new();
    }

    public class EmployeeTimeSummaryDto
    {
        public long UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal EstHours { get; set; }
        public decimal CompletedHours { get; set; }
        public decimal Variance { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class ProjectTimeSummaryDto
    {
        public long ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal EstHours { get; set; }
        public decimal CompletedHours { get; set; }
        public decimal Variance { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class TaskTimeDetailDto
    {
        public long TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public long ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public long? AssignedTo { get; set; }
        public string AssigneeName { get; set; } = string.Empty;
        public string AssigneeUsername { get; set; } = string.Empty;
        public decimal? EstimatedHours { get; set; }
        public decimal CompletedHours { get; set; }
        public DateTime DueDate { get; set; }
        public long StatusId { get; set; }
    }
}
