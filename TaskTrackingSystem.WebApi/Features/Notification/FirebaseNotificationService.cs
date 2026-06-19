using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TaskTrackingSystem.Database.AppDbContextModels;
using TaskTrackingSystem.Shared.Models.Notification;
using TaskTrackingSystem.Shared.Enums;
using DbNotification = TaskTrackingSystem.Database.AppDbContextModels.Notification;

namespace TaskTrackingSystem.WebApi.Features.Notification;

public class FirebaseNotificationService
{
    private static readonly Regex MentionRegex = new(@"@(?<username>[A-Za-z0-9._]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly SemaphoreSlim AccessTokenLock = new(1, 1);
    private static string? CachedAccessToken;
    private static DateTimeOffset CachedAccessTokenExpiresAt;

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationRealtimeService _realtimeService;
    private readonly string _serviceAccountPath;

    public FirebaseNotificationService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        NotificationRealtimeService realtimeService,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _realtimeService = realtimeService;

        var configuredPath = configuration["Firebase:ServiceAccountPath"] ?? "Firebase/ttsfirebasekey.json";
        _serviceAccountPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public global::System.Threading.Tasks.Task NotifyTaskAssignedAsync(Database.AppDbContextModels.Task task, long senderId)
    {
        if (!task.AssignedTo.HasValue)
        {
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        var title = "Task assigned";
        var body = $"You have been assigned a new task: {task.Title}";

        return SendAsync(
            recipientIds: new[] { task.AssignedTo.Value },
            senderId: senderId,
            notificationType: NotificationType.TaskAssigned,
            sourceType: "task",
            sourceId: task.Id,
            title: title,
            body: body);
    }

    public async global::System.Threading.Tasks.Task NotifyTaskStatusChangedAsync(Database.AppDbContextModels.Task task, long senderId, AppTaskStatus oldStatusId, AppTaskStatus newStatusId)
    {
        var recipientIds = BuildTaskAudience(task, senderId);
        if (recipientIds.Count == 0)
        {
            return;
        }

        var title = "Task status changed";
        var body = $"Task '{task.Title}' changed from {GetStatusLabel(oldStatusId)} to {GetStatusLabel(newStatusId)}";

        await SendAsync(
            recipientIds,
            senderId,
            NotificationType.StatusChanged,
            "task",
            task.Id,
            title,
            body);
    }

    public async global::System.Threading.Tasks.Task NotifyProjectAssignedAsync(Database.AppDbContextModels.Project project, IEnumerable<long> recipientIds, long senderId)
    {
        var uniqueRecipientIds = recipientIds
            .Where(id => id > 0 && id != senderId)
            .Distinct()
            .ToList();

        if (uniqueRecipientIds.Count == 0)
        {
            return;
        }

        var title = "Project assigned";
        var body = $"You have been assigned to project '{project.Name}'";

        await SendAsync(
            uniqueRecipientIds,
            senderId,
            NotificationType.ProjectUpdated,
            "project",
            project.Id,
            title,
            body);
    }

    public async global::System.Threading.Tasks.Task NotifyCommentAddedAsync(Database.AppDbContextModels.Task task, long senderId, string actorName, string message)
    {
        var recipientIds = BuildTaskAudience(task, senderId);
        var mentionedIds = await FindMentionedUserIdsAsync(task.ProjectId, senderId, message);
        if (mentionedIds.Count > 0)
        {
            recipientIds = recipientIds
                .Except(mentionedIds)
                .ToList();
        }

        if (recipientIds.Count == 0)
        {
            return;
        }

        var title = "New comment";
        var body = $"{actorName} commented on task '{task.Title}'";

        await SendAsync(
            recipientIds,
            senderId,
            NotificationType.CommentAdded,
            "task",
            task.Id,
            title,
            body);
    }

    public async global::System.Threading.Tasks.Task NotifyMentionedAsync(Database.AppDbContextModels.Task task, long senderId, string actorName, string message)
    {
        var recipientIds = await FindMentionedUserIdsAsync(task.ProjectId, senderId, message);
        if (recipientIds.Count == 0)
        {
            return;
        }

        var title = "You were mentioned";
        var body = $"{actorName} mentioned you in task '{task.Title}'";

        await SendAsync(
            recipientIds,
            senderId,
            NotificationType.Mention,
            "task",
            task.Id,
            title,
            body);
    }

    private List<long> BuildTaskAudience(Database.AppDbContextModels.Task task, long senderId)
    {
        var audience = new HashSet<long>();

        if (task.AssignedTo.HasValue && task.AssignedTo.Value != senderId)
        {
            audience.Add(task.AssignedTo.Value);
        }

        if (task.AssignedBy.HasValue && task.AssignedBy.Value != senderId)
        {
            audience.Add(task.AssignedBy.Value);
        }

        if (task.CreatedBy.HasValue && task.CreatedBy.Value != senderId)
        {
            audience.Add(task.CreatedBy.Value);
        }

        return audience.ToList();
    }

    private async global::System.Threading.Tasks.Task<List<long>> FindMentionedUserIdsAsync(long projectId, long senderId, string message)
    {
        var usernames = MentionRegex.Matches(message)
            .Select(match => match.Groups["username"].Value)
            .Where(username => !string.IsNullOrWhiteSpace(username))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (usernames.Count == 0)
        {
            return new List<long>();
        }

        var projectMemberIds = await _db.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .Select(pm => pm.UserId)
            .ToListAsync();

        if (projectMemberIds.Count == 0)
        {
            return new List<long>();
        }

        return await _db.Users
            .Where(u => u.Id != senderId && projectMemberIds.Contains(u.Id) && usernames.Contains(u.Username))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync();
    }

    private async global::System.Threading.Tasks.Task SendAsync(
        IEnumerable<long> recipientIds,
        long senderId,
        NotificationType notificationType,
        string sourceType,
        long sourceId,
        string title,
        string body)
    {
        var uniqueRecipientIds = recipientIds.Distinct().ToList();
        if (uniqueRecipientIds.Count == 0)
        {
            return;
        }

        var notifications = uniqueRecipientIds.Select(recipientId => new DbNotification
        {
            RecipientId = recipientId,
            SenderId = senderId > 0 ? senderId : null,
            NotificationType = (byte)notificationType,
            Title = title,
            Body = body,
            SourceType = sourceType,
            SourceId = sourceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();

        var senderName = senderId > 0
            ? await _db.Users
                .Where(u => u.Id == senderId)
                .Select(u => $"{u.FirstName} {u.LastName}")
                .FirstOrDefaultAsync()
            : null;

        var unreadCounts = await _db.Notifications
            .Where(n => uniqueRecipientIds.Contains(n.RecipientId) && !n.IsRead)
            .GroupBy(n => n.RecipientId)
            .Select(g => new { RecipientId = g.Key, Count = g.Count() })
            .ToListAsync();
        var unreadCountLookup = unreadCounts.ToDictionary(x => x.RecipientId, x => x.Count);

        foreach (var notification in notifications)
        {
            var dto = new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Body = notification.Body,
                NotificationType = notification.NotificationType,
                SourceType = notification.SourceType,
                SourceId = notification.SourceId,
                TargetUrl = NotificationNavigation.BuildTargetUrl(notification.SourceType, notification.SourceId, notification.NotificationType),
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                SenderName = senderName
            };

            var recipientUnreadCount = unreadCountLookup.TryGetValue(notification.RecipientId, out var unreadCount)
                ? unreadCount
                : 0;

            await _realtimeService.SendCreatedAsync(notification.RecipientId, dto, recipientUnreadCount);
        }

        var tokens = await _db.UserDevices
            .Where(d => uniqueRecipientIds.Contains(d.UserId))
            .Select(d => d.FcmToken)
            .Distinct()
            .ToListAsync();

        foreach (var token in tokens)
        {
            await SendPushAsync(token, title, body, sourceType, sourceId, notificationType);
        }
    }

    private async global::System.Threading.Tasks.Task SendPushAsync(
        string token,
        string title,
        string body,
        string sourceType,
        long sourceId,
        NotificationType notificationType)
    {
        var serviceAccount = await LoadServiceAccountAsync();
        var accessToken = await GetAccessTokenAsync(serviceAccount);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{serviceAccount.ProjectId}/messages:send");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var payload = new
        {
            message = new
            {
                token,
                notification = new
                {
                    title,
                    body
                },
                data = new Dictionary<string, string>
                {
                    ["sourceType"] = sourceType,
                    ["sourceId"] = sourceId.ToString(),
                    ["notificationType"] = ((byte)notificationType).ToString(),
                    ["targetUrl"] = NotificationNavigation.BuildTargetUrl(sourceType, sourceId, (byte)notificationType)
                }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Firebase] Failed to send push: {(int)response.StatusCode} {response.ReasonPhrase}. {error}");
        }
    }

    private async global::System.Threading.Tasks.Task<string> GetAccessTokenAsync(FirebaseServiceAccount serviceAccount)
    {
        if (!string.IsNullOrWhiteSpace(CachedAccessToken) && CachedAccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return CachedAccessToken!;
        }

        await AccessTokenLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(CachedAccessToken) && CachedAccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return CachedAccessToken!;
            }

            var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = iat + 3600;

            var headerJson = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
            var claimJson = JsonSerializer.Serialize(new
            {
                iss = serviceAccount.ClientEmail,
                scope = "https://www.googleapis.com/auth/firebase.messaging",
                aud = "https://oauth2.googleapis.com/token",
                iat,
                exp
            });

            var unsignedJwt = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson))}.{Base64UrlEncode(Encoding.UTF8.GetBytes(claimJson))}";
            var signature = SignJwt(unsignedJwt, serviceAccount.PrivateKey);
            var assertion = $"{unsignedJwt}.{Base64UrlEncode(signature)}";

            var client = _httpClientFactory.CreateClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", assertion)
            });

            using var response = await client.PostAsync("https://oauth2.googleapis.com/token", form);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Firebase access token response did not contain an access_token.");
            }

            CachedAccessToken = token;
            CachedAccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            return token;
        }
        finally
        {
            AccessTokenLock.Release();
        }
    }

    private async global::System.Threading.Tasks.Task<FirebaseServiceAccount> LoadServiceAccountAsync()
    {
        var json = await File.ReadAllTextAsync(_serviceAccountPath);
        var serviceAccount = JsonSerializer.Deserialize<FirebaseServiceAccount>(json);
        if (serviceAccount == null ||
            string.IsNullOrWhiteSpace(serviceAccount.ProjectId) ||
            string.IsNullOrWhiteSpace(serviceAccount.ClientEmail) ||
            string.IsNullOrWhiteSpace(serviceAccount.PrivateKey))
        {
            throw new InvalidOperationException("Firebase service account file is missing required fields.");
        }

        return serviceAccount;
    }

    private static byte[] SignJwt(string unsignedJwt, string privateKey)
    {
        var pem = privateKey
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("-----END PRIVATE KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        var keyBytes = Convert.FromBase64String(pem);
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);

        return rsa.SignData(Encoding.ASCII.GetBytes(unsignedJwt), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GetStatusLabel(AppTaskStatus statusId) => statusId switch
    {
        AppTaskStatus.Todo => "To Do",
        AppTaskStatus.InProgress => "In Progress",
        AppTaskStatus.Done => "Done",
        _ => $"Status {statusId}"
    };

    private sealed class FirebaseServiceAccount
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("client_email")]
        public string ClientEmail { get; set; } = string.Empty;

        [JsonPropertyName("private_key")]
        public string PrivateKey { get; set; } = string.Empty;
    }
}
