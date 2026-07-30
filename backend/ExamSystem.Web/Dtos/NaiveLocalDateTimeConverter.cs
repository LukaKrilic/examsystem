using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExamSystem.Web.Dtos;

// The spec contract for Timestamp fields is a naive Europe/Zagreb local date-time — but the default
// DateTime converter accepts ISO 8601 strings WITH an offset/"Z" too, and silently reinterprets them
// (the ±10 min clock-skew guard then masks the resulting multi-hour error by falling back to server
// time, without ever surfacing that the client sent something other than the agreed contract). This
// converter strips any trailing offset/"Z" and parses only the wall-clock digits, so the value is
// always read as literal Zagreb local time regardless of what a client appends.
public class NaiveLocalDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Expected a date-time string");
        var tIndex = s.IndexOf('T');
        var searchFrom = tIndex >= 0 ? tIndex + 1 : s.Length;
        var offsetIndex = s.IndexOfAny(['Z', 'z', '+', '-'], searchFrom);
        var wallClock = offsetIndex >= 0 ? s[..offsetIndex] : s;
        return DateTime.Parse(wallClock, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
}
