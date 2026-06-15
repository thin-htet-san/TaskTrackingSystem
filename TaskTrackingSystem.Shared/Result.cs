using System;

namespace TaskTrackingSystem.Shared
{
    public class Result<T>
    {
        public bool IsSuccess { get; set; }
        public T? Value { get; set; }
        public string? ErrorMessage { get; set; }
        public int StatusCode { get; set; } // 200, 400, 404, etc.

        public static Result<T> Success(T value, int statusCode = 200)
        {
            return new Result<T>
            {
                IsSuccess = true,
                Value = value,
                StatusCode = statusCode
            };
        }

        public static Result<T> Failure(string errorMessage, int statusCode = 400)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                StatusCode = statusCode
            };
        }
    }

    public class Result
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public int StatusCode { get; set; }

        public static Result Success(int statusCode = 200)
        {
            return new Result
            {
                IsSuccess = true,
                StatusCode = statusCode
            };
        }

        public static Result Failure(string errorMessage, int statusCode = 400)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                StatusCode = statusCode
            };
        }
    }

    public static class ResultMessages
    {
        // Auth
        public const string InvalidCredentials = "Invalid username/email or password.";
        public const string ParseAuthResponseFailed = "Failed to parse authentication response tokens.";

        // Project
        public const string ProjectNameRequired = "Project name is required.";
        public const string FailedToUpdateProject = "Failed to update project.";
        public const string FailedToCreateProject = "Failed to create project.";
        public static string ProjectNotFound(long id) => $"Project with ID {id} not found.";
        public const string UserIdsCannotBeNull = "User IDs cannot be null.";
        public static string InvalidUserIds(string ids) => $"The following user IDs are invalid or deleted: {ids}";
        public static string UserNotProjectMember(long userId, long projectId) => $"User with ID {userId} is not a member of project with ID {projectId}.";

        // Task
        public const string TaskTitleRequired = "Task title is required.";
        public const string SelectProjectRequired = "Please select a project.";
        public const string FailedToUpdateTask = "Failed to update task.";
        public const string FailedToCreateTask = "Failed to create task.";
        public static string TaskNotFound(long id) => $"Task with ID {id} not found.";
        public static string TaskNotFoundOrDeleted(long id) => $"Task with ID {id} not found or already deleted.";

        // Role/Permission
        public const string RoleNameRequired = "Role name is required.";
        public const string FailedToUpdateRole = "Failed to update role.";
        public const string FailedToCreateRole = "Failed to create role.";
        public static string RoleNotFound(long id) => $"Role with ID {id} not found.";
        public const string PermissionIdsCannotBeNull = "Permission IDs cannot be null.";
        public static string InvalidPermissionIds(string ids) => $"The following permission IDs are invalid or deleted: {ids}";

        // User
        public const string FillAllFields = "Please fill in all required fields.";
        public const string FailedToUpdateUser = "Failed to update user.";
        public const string PasswordMinLength = "Password must be at least 6 characters.";
        public const string FailedToCreateUser = "Failed to create user.";
        public static string UserNotFound(long id) => $"User with ID {id} not found.";

        // Register
        public const string ParseRegistrationResponseFailed = "Failed to parse registration response.";
        public const string RegistrationFailed = "Registration failed. Please try again.";

        // Username Validation Rules
        public const string UsernameMinLength = "Username must be at least 3 characters long.";
        public const string UsernameNoSpaces = "Username cannot contain spaces.";
        public const string UsernameInvalidCharacters = "Username cannot contain spaces, and can only contain letters, numbers, underscores (_), and periods (.).";

        // Password Validation Rules
        public const string PasswordMinLengthRule = "Password must be at least 8 characters long.";
        public const string PasswordComplexityRule = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.";
    }
}
