using System.Net;
using System.Net.Http.Json;

namespace ExamSystem.Web.Infoeduka;

// Typed HttpClient over MockInfoeduka.Api (BaseAddress from Infoeduka:BaseUrl). A 404 means "Infoeduka
// doesn't have this" and becomes null; anything else — timeouts, 500s, a dead process — still throws,
// because an external dependency failing must never be silently masked as "no data" (CLAUDE.md rule 9).
public class HttpInfoedukaClient(HttpClient http) : IInfoedukaClient
{
    public Task<InfoedukaStudent?> GetStudentAsync(string studentId, CancellationToken ct = default)
        => GetOrNullAsync<InfoedukaStudent>($"/api/students/{studentId}", ct);

    public Task<InfoedukaStudent?> ResolveStudentByIdentityAsync(
        string? aaiPrincipal, string? jmbag, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (aaiPrincipal is not null) query.Add($"aaiPrincipal={Uri.EscapeDataString(aaiPrincipal)}");
        if (jmbag is not null) query.Add($"jmbag={Uri.EscapeDataString(jmbag)}");

        return GetOrNullAsync<InfoedukaStudent>($"/api/students/resolve?{string.Join('&', query)}", ct);
    }

    public async Task<IReadOnlyList<InfoedukaRegistration>> GetRegistrationsAsync(
        string studentId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default)
    {
        // Each bound is applied independently, so passing only one narrows the list instead of being
        // silently dropped. Round-trip ("O") format keeps the offset intact through the query string.
        var query = new List<string>();
        if (from is not null) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to is not null) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");

        var url = $"/api/students/{studentId}/registrations";
        if (query.Count > 0) url += $"?{string.Join('&', query)}";

        return await http.GetFromJsonAsync<List<InfoedukaRegistration>>(url, ct) ?? [];
    }

    public Task<InfoedukaCourseOutcomes?> GetCourseOutcomesAsync(
        string studentId, string courseId, CancellationToken ct = default)
        => GetOrNullAsync<InfoedukaCourseOutcomes>($"/api/students/{studentId}/courses/{courseId}/outcomes", ct);

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken ct)
    {
        var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
}
