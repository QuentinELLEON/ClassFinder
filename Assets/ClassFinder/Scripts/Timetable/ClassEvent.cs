using System;

namespace ClassFinder.Timetable
{
    /// <summary>
    /// One class session parsed from the university iCal feed (ADE).
    /// All times are stored in UTC; use the *Local helpers for display.
    /// </summary>
    [Serializable]
    public class ClassEvent
    {
        public string Uid = string.Empty;
        public string Summary = string.Empty;      // e.g. "Algorithmique - CM"
        public string Location = string.Empty;     // e.g. "Templiers - B2.14" (may be empty)
        public string Description = string.Empty;  // groups, teacher... (export footer removed)
        public DateTime StartUtc;
        public DateTime EndUtc;

        public DateTime StartLocal => StartUtc.ToLocalTime();
        public DateTime EndLocal => EndUtc.ToLocalTime();

        public bool HasLocation => !string.IsNullOrWhiteSpace(Location);

        public bool IsOngoing(DateTime nowUtc) => StartUtc <= nowUtc && EndUtc > nowUtc;

        public bool IsFinished(DateTime nowUtc) => EndUtc <= nowUtc;

        public override string ToString() =>
            $"{StartLocal:yyyy-MM-dd HH:mm}-{EndLocal:HH:mm} {Summary} @ {Location}";
    }
}
