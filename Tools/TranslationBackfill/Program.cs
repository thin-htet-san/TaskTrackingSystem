using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using TaskTrackingSystem.Shared.Localization;

var options = BackfillOptions.Parse(args);
if (options.ShowHelp)
{
    BackfillOptions.PrintHelp();
    return 0;
}

var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Missing SUPABASE_CONNECTION_STRING or ConnectionStrings__DefaultConnection.");
    return 2;
}

if (!options.Apply)
{
    Console.WriteLine("Dry run mode: no database updates will be written. Use --apply to persist translations.");
}

var provider = new BackfillTranslationProvider();
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(options.CancellationToken);

var targets = BackfillTarget.Create(options);
var summary = new BackfillSummary();

foreach (var target in targets)
{
    var rows = await ReadRowsAsync(connection, target, options);
    foreach (var batch in rows.Chunk(options.BatchSize))
    {
        foreach (var row in batch)
        {
            foreach (var field in target.Fields)
            {
                if (string.IsNullOrWhiteSpace(row.Values[field.Source]) || !string.IsNullOrWhiteSpace(row.Values[field.Target]))
                {
                    continue;
                }

                summary.Inspected++;
                var source = row.Values[field.Source]!;
                var result = await provider.TranslateAsync(
                    source,
                    field.IsName ? "my" : "en",
                    field.IsName ? "en" : "my",
                    field.IsName,
                    options.CancellationToken);

                if (!result.Success || string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    summary.Failed++;
                    Console.WriteLine($"{target.Table} {row.Id} {field.Source}->{field.Target}: failed provider={result.Provider} error={result.ErrorMessage}");
                    continue;
                }

                if (!options.Apply)
                {
                    summary.WouldUpdate++;
                    Console.WriteLine($"{target.Table} {row.Id} {field.Source}->{field.Target}: would update provider={result.Provider}");
                    continue;
                }

                try
                {
                    var updated = await UpdateMissingFieldAsync(connection, target, row.Id, field, result.TranslatedText!, options.CancellationToken);
                    if (updated)
                    {
                        summary.Updated++;
                        Console.WriteLine($"{target.Table} {row.Id} {field.Source}->{field.Target}: updated provider={result.Provider}");
                    }
                    else
                    {
                        summary.Skipped++;
                        Console.WriteLine($"{target.Table} {row.Id} {field.Source}->{field.Target}: skipped because target was filled concurrently");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    summary.Failed++;
                    Console.WriteLine($"{target.Table} {row.Id} {field.Source}->{field.Target}: failed database update error={ex.Message}");
                }
            }
        }
    }
}

Console.WriteLine($"Summary: inspected={summary.Inspected}; wouldUpdate={summary.WouldUpdate}; updated={summary.Updated}; skipped={summary.Skipped}; failed={summary.Failed}; mode={(options.Apply ? "apply" : "dry-run")}");
return 0;

static async Task<List<BackfillRow>> ReadRowsAsync(NpgsqlConnection connection, BackfillTarget target, BackfillOptions options)
{
    var missing = string.Join(" OR ", target.Fields.Select(field => $"(\"{field.Target}\" IS NULL OR btrim(\"{field.Target}\") = '')"));
    var sql = $"SELECT \"Id\", {string.Join(", ", target.Fields.SelectMany(field => new[] { $"\"{field.Source}\"", $"\"{field.Target}\"" }).Distinct())} FROM \"{target.Table}\" WHERE ({string.Join(" OR ", target.Fields.Select(field => $"\"{field.Source}\" IS NOT NULL AND btrim(\"{field.Source}\") <> ''"))}) AND ({missing})";
    if (options.Id.HasValue) sql += " AND \"Id\" = @id";
    sql += " ORDER BY \"Id\" LIMIT @limit";

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("limit", options.Limit ?? int.MaxValue);
    if (options.Id.HasValue) command.Parameters.AddWithValue("id", options.Id.Value);

    var rows = new List<BackfillRow>();
    await using var reader = await command.ExecuteReaderAsync(options.CancellationToken);
    while (await reader.ReadAsync(options.CancellationToken))
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < reader.FieldCount; index++)
        {
            values[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetString(index);
        }
        rows.Add(new(reader.GetInt64(0), values));
    }
    return rows;
}

static async Task<bool> UpdateMissingFieldAsync(NpgsqlConnection connection, BackfillTarget target, long id, BackfillField field, string translatedText, CancellationToken cancellationToken)
{
    var sql = $"UPDATE \"{target.Table}\" SET \"{field.Target}\" = @value WHERE \"Id\" = @id AND (\"{field.Target}\" IS NULL OR btrim(\"{field.Target}\") = '')";
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("value", translatedText);
    command.Parameters.AddWithValue("id", id);
    return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
}

internal sealed record BackfillTarget(string Table, IReadOnlyList<BackfillField> Fields)
{
    public static IReadOnlyList<BackfillTarget> Create(BackfillOptions options)
    {
        var all = new List<BackfillTarget>
        {
            new("Projects", [new("Name", "NameMy"), new("Description", "DescriptionMy")]),
            new("Tasks", [new("Title", "TitleMy"), new("Description", "DescriptionMy")]),
            new("Issues", [new("Title", "TitleMy"), new("Description", "DescriptionMy"), new("DelayReason", "DelayReasonMy"), new("BlockedBy", "BlockedByMy")]),
            new("Users", [new("FirstName", "FirstNameMy", true), new("LastName", "LastNameMy", true)]),
            new("Roles", [new("Name", "NameMy"), new("Description", "DescriptionMy")]),
            new("notifications", [new("Title", "TitleMy"), new("Body", "BodyMy")])
        };
        if (options.IncludeComments) all.Add(new("Comments", [new("Message", "MessageMy")]));
        return options.Table is null ? all : all.Where(target => string.Equals(target.Table, options.Table, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}

internal sealed record BackfillField(string Source, string Target, bool IsName = false);
internal sealed record BackfillRow(long Id, Dictionary<string, string?> Values);
internal sealed class BackfillSummary { public int Inspected; public int WouldUpdate; public int Updated; public int Skipped; public int Failed; }

internal sealed class BackfillOptions
{
    public bool Apply { get; private set; }
    public bool IncludeComments { get; private set; }
    public bool ShowHelp { get; private set; }
    public string? Table { get; private set; }
    public int? Limit { get; private set; }
    public int BatchSize { get; private set; } = 20;
    public long? Id { get; private set; }
    public CancellationToken CancellationToken => cancellationSource.Token;
    private readonly CancellationTokenSource cancellationSource = new();

    public static BackfillOptions Parse(string[] args)
    {
        var options = new BackfillOptions();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; options.cancellationSource.Cancel(); };
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--apply": options.Apply = true; break;
                case "--dry-run": options.Apply = false; break;
                case "--include-comments": options.IncludeComments = true; break;
                case "--table" when i + 1 < args.Length: options.Table = args[++i]; break;
                case "--limit" when i + 1 < args.Length && int.TryParse(args[++i], out var limit): options.Limit = Math.Max(1, limit); break;
                case "--batch-size" when i + 1 < args.Length && int.TryParse(args[++i], out var batchSize): options.BatchSize = Math.Max(1, batchSize); break;
                case "--id" when i + 1 < args.Length && long.TryParse(args[++i], out var id): options.Id = id; break;
                case "--help" or "-h": options.ShowHelp = true; break;
            }
        }
        return options;
    }

    public static void PrintHelp() => Console.WriteLine("Usage: dotnet run --project Tools/TranslationBackfill -- --dry-run|--apply [--table Projects] [--batch-size 20] [--limit 100] [--id 123] [--include-comments]");
}

internal sealed class BackfillTranslationProvider
{
    private readonly HttpClient client = new();
    private readonly string? endpoint = Environment.GetEnvironmentVariable("TRANSLATION_API_URL");
    private readonly string? apiKey = Environment.GetEnvironmentVariable("TRANSLATION_API_KEY");
    private readonly string provider = Environment.GetEnvironmentVariable("TRANSLATION_PROVIDER") ?? "configured-http";

    public async Task<TranslationResult> TranslateAsync(string sourceText, string sourceLanguage, string targetLanguage, bool isName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return TranslationResult.NotConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new { text = sourceText, sourceLanguage, targetLanguage, mode = isName ? "transliteration" : "translation", instruction = isName ? "Return the name only; transliterate pronunciation, do not translate meaning." : "Return translated text only; preserve names, codes, IDs, dates, numbers, placeholders, URLs, and appropriate technical terms." });
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return new(false, null, $"Provider returned {(int)response.StatusCode}.", provider, false);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var text = root.TryGetProperty("translatedText", out var translated) ? translated.GetString() : root.TryGetProperty("text", out var plain) ? plain.GetString() : root.TryGetProperty("translation", out var alternate) ? alternate.GetString() : null;
            return string.IsNullOrWhiteSpace(text) ? new(false, null, "Provider returned no translated text.", provider, false) : TranslationResult.Generated(text.Trim(), provider);
        }
        catch (JsonException) { return new(false, null, "Provider returned invalid JSON.", provider, false); }
    }
}
