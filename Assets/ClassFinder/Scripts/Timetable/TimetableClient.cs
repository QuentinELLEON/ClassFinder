using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Networking;

namespace ClassFinder.Timetable
{
    /// <summary>
    /// Downloads a student's timetable from the Université Côte d'Azur ADE anonymous iCal endpoint.
    /// The "code" query parameter is the student ID.
    /// </summary>
    public static class TimetableClient
    {
        public const string DefaultBaseUrl =
            "https://edt.univ-cotedazur.fr/jsp/custom/modules/plannings/anonymous_cal.jsp";

        public static string BuildUrl(string baseUrl, string studentId, int projectId, DateTime firstDate, DateTime lastDate)
        {
            var inv = CultureInfo.InvariantCulture;
            return $"{baseUrl}?code={Uri.EscapeDataString(studentId)}" +
                   $"&projectId={projectId.ToString(inv)}" +
                   "&calType=ical" +
                   $"&firstDate={firstDate.ToString("yyyy-MM-dd", inv)}" +
                   $"&lastDate={lastDate.ToString("yyyy-MM-dd", inv)}";
        }

        /// <summary>
        /// Coroutine: fetches events from today to today + daysAhead (local dates).
        /// Calls exactly one of onSuccess / onError.
        /// </summary>
        public static IEnumerator FetchEvents(
            string studentId,
            Action<List<ClassEvent>> onSuccess,
            Action<string> onError,
            int daysAhead = 7,
            int projectId = 6,
            string baseUrl = DefaultBaseUrl,
            int timeoutSeconds = 20)
        {
            var today = DateTime.Now.Date;
            var url = BuildUrl(baseUrl, studentId, projectId, today, today.AddDays(daysAhead));

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.responseCode > 0
                        ? $"Server error ({request.responseCode}). Check your student ID."
                        : $"Network error: {request.error}");
                    yield break;
                }

                var body = request.downloadHandler.text;
                if (string.IsNullOrEmpty(body) || body.IndexOf("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    onError?.Invoke("No timetable found for this student ID.");
                    yield break;
                }

                List<ClassEvent> events;
                try
                {
                    events = ICalParser.Parse(body);
                }
                catch (Exception e)
                {
                    onError?.Invoke("Could not read the timetable: " + e.Message);
                    yield break;
                }

                onSuccess?.Invoke(events);
            }
        }
    }
}
