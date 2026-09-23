using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClassFinder.Timetable
{
    /// <summary>
    /// Minimal RFC 5545 parser, enough for the ADE (edt.univ-cotedazur.fr) export:
    /// line unfolding, VEVENT blocks, DTSTART/DTEND in UTC ("Z"), with TZID, floating or date-only,
    /// and text unescaping. No external dependency, so it runs on every Unity platform.
    /// </summary>
    public static class ICalParser
    {
        public static List<ClassEvent> Parse(string ics)
        {
            var events = new List<ClassEvent>();
            if (string.IsNullOrEmpty(ics)) return events;

            ClassEvent current = null;
            bool hasStart = false, hasEnd = false;

            foreach (var line in Unfold(ics))
            {
                if (line.Length == 0) continue;

                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    current = new ClassEvent();
                    hasStart = hasEnd = false;
                    continue;
                }

                if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (current != null && hasStart)
                    {
                        if (!hasEnd || current.EndUtc < current.StartUtc)
                            current.EndUtc = current.StartUtc.AddHours(1);
                        events.Add(current);
                    }
                    current = null;
                    continue;
                }

                if (current == null) continue;
                if (!TrySplitProperty(line, out var name, out var parameters, out var value)) continue;

                switch (name)
                {
                    case "SUMMARY":
                        current.Summary = Unescape(value).Trim();
                        break;
                    case "LOCATION":
                        current.Location = Unescape(value).Trim();
                        break;
                    case "DESCRIPTION":
                        current.Description = CleanDescription(Unescape(value));
                        break;
                    case "UID":
                        current.Uid = value.Trim();
                        break;
                    case "DTSTART":
                        if (TryParseDate(value, parameters, out var start)) { current.StartUtc = start; hasStart = true; }
                        break;
                    case "DTEND":
                        if (TryParseDate(value, parameters, out var end)) { current.EndUtc = end; hasEnd = true; }
                        break;
                }
            }

            events.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
            return events;
        }

        /// <summary>Returns the class happening now, or else the next one to start. Null if none.</summary>
        public static ClassEvent FindNext(IList<ClassEvent> sortedEvents, DateTime nowUtc)
        {
            if (sortedEvents == null) return null;
            foreach (var e in sortedEvents)
                if (e.EndUtc > nowUtc) return e;
            return null;
        }

        // Lines beginning with a space or tab continue the previous line (RFC 5545 §3.1).
        static IEnumerable<string> Unfold(string ics)
        {
            var lines = ics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var sb = new StringBuilder();
            bool any = false;
            foreach (var raw in lines)
            {
                if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t'))
                {
                    sb.Append(raw, 1, raw.Length - 1);
                    continue;
                }
                if (any) yield return sb.ToString();
                sb.Clear();
                sb.Append(raw);
                any = true;
            }
            if (any) yield return sb.ToString();
        }

        // "DTSTART;TZID=Europe/Paris:20260922T080000" -> name, params, value
        static bool TrySplitProperty(string line, out string name, out Dictionary<string, string> parameters, out string value)
        {
            name = value = null;
            parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int colon = -1;
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ':' && !inQuotes) { colon = i; break; }
            }
            if (colon < 0) return false;

            value = line.Substring(colon + 1);
            var head = line.Substring(0, colon).Split(';');
            name = head[0].Trim().ToUpperInvariant();
            for (int i = 1; i < head.Length; i++)
            {
                int eq = head[i].IndexOf('=');
                if (eq > 0)
                    parameters[head[i].Substring(0, eq).Trim()] = head[i].Substring(eq + 1).Trim('"');
            }
            return true;
        }

        static bool TryParseDate(string value, Dictionary<string, string> parameters, out DateTime utc)
        {
            utc = default(DateTime);
            value = value.Trim();
            var inv = CultureInfo.InvariantCulture;

            // UTC: 20260922T080000Z
            if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) &&
                DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", inv,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
            {
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
                return true;
            }

            // Local / TZID: 20260922T100000
            if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", inv, DateTimeStyles.None, out var local))
            {
                utc = ToUtc(local, parameters.TryGetValue("TZID", out var tzid) ? tzid : null);
                return true;
            }

            // All-day: 20260922
            if (DateTime.TryParseExact(value, "yyyyMMdd", inv, DateTimeStyles.None, out var day))
            {
                utc = ToUtc(day, null);
                return true;
            }
            return false;
        }

        static DateTime ToUtc(DateTime unspecified, string tzid)
        {
            unspecified = DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified);
            if (!string.IsNullOrEmpty(tzid))
            {
                // IANA id works on Android/iOS/macOS; Windows editor may need the Windows id.
                foreach (var id in new[] { tzid, tzid == "Europe/Paris" ? "Romance Standard Time" : null })
                {
                    if (id == null) continue;
                    try
                    {
                        var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
                    }
                    catch (Exception) { /* try next / fall back */ }
                }
            }
            // Floating time: interpret in the device's time zone.
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Local).ToUniversalTime();
        }

        static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    switch (n)
                    {
                        case 'n': case 'N': sb.Append('\n'); break;
                        default: sb.Append(n); break; // \, \; \\
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ADE descriptions look like "\nL3 Informatique\nDUPONT Jean\n(Exporté le:22/09/2026 10:31)\n".
        static string CleanDescription(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var kept = new List<string>();
            foreach (var part in s.Split('\n'))
            {
                var line = part.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("(Export", StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(line);
            }
            return string.Join("\n", kept);
        }
    }
}
