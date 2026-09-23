using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ClassFinder.Timetable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClassFinder.UI
{
    /// <summary>
    /// Top-right widget:
    ///  1. no student ID saved -> small "Student ID" box (input + Save)
    ///  2. ID saved (PlayerPrefs, survives restarts) -> "Next class" card + "Go to next class" button.
    /// Just add this component to any GameObject: if no UI is assigned, it builds its own at runtime.
    /// </summary>
    public class NextClassController : MonoBehaviour
    {
        public const string StudentIdPrefKey = "ClassFinder.StudentId";

        [Header("Timetable source")]
        [SerializeField] string baseUrl = TimetableClient.DefaultBaseUrl;
        [SerializeField] int projectId = 6;
        [Tooltip("How many days ahead to look for the next class.")]
        [SerializeField, Range(1, 60)] int daysAhead = 7;
        [Tooltip("Re-download the timetable every N minutes (0 = never).")]
        [SerializeField] float autoRefreshMinutes = 15f;

        [Header("Student ID box (auto-built if empty)")]
        [SerializeField] internal GameObject studentIdPanel;
        [SerializeField] internal TMP_InputField studentIdInput;
        [SerializeField] internal Button confirmButton;
        [SerializeField] internal TMP_Text studentIdErrorText;

        [Header("Next class card (auto-built if empty)")]
        [SerializeField] internal GameObject nextClassPanel;
        [SerializeField] internal TMP_Text headerText;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text roomText;
        [SerializeField] internal TMP_Text timeText;
        [SerializeField] internal TMP_Text countdownText;
        [SerializeField] internal Button goToNextClassButton;
        [SerializeField] internal Button changeIdButton;

        /// <summary>Raised whenever the displayed class changes (null = no upcoming class).</summary>
        public event Action<ClassEvent> NextClassChanged;

        public ClassEvent NextClass { get; private set; }
        public string StudentId { get; private set; }
        public IReadOnlyList<ClassEvent> Events => events;

        readonly List<ClassEvent> events = new List<ClassEvent>();
        static readonly CultureInfo DisplayCulture = CultureInfo.InvariantCulture;
        bool isLoading;
        bool cardDirty;
        float nextTickTime;
        float nextRefreshTime = float.MaxValue;

        void Awake()
        {
            if (studentIdPanel == null && nextClassPanel == null)
                NextClassWidgetBuilder.Build(this);

            if (confirmButton) confirmButton.onClick.AddListener(OnConfirmStudentId);
            if (studentIdInput) studentIdInput.onSubmit.AddListener(_ => OnConfirmStudentId());
            if (goToNextClassButton) goToNextClassButton.onClick.AddListener(OnGoToNextClass);
            if (changeIdButton) changeIdButton.onClick.AddListener(ShowStudentIdPanel);
        }

        void Start()
        {
            var saved = PlayerPrefs.GetString(StudentIdPrefKey, string.Empty);
            if (string.IsNullOrEmpty(saved))
            {
                ShowStudentIdPanel();
            }
            else
            {
                StudentId = saved;
                ShowNextClassPanel();
                Refresh();
            }
        }

        void Update()
        {
            if (Time.unscaledTime >= nextTickTime)
            {
                nextTickTime = Time.unscaledTime + 1f;
                UpdateNextClass();
            }

            if (!isLoading && !string.IsNullOrEmpty(StudentId) && Time.unscaledTime >= nextRefreshTime)
                Refresh();
        }

        // ---------- Buttons ----------

        void OnGoToNextClass()
        {
            // Placeholder: navigation is not implemented yet.
            Debug.Log("[ClassFinder] 'Go to next class' pressed" +
                      (NextClass != null ? $" -> {NextClass.Location}" : string.Empty));
        }

        // ---------- Student ID ----------

        public void ShowStudentIdPanel()
        {
            if (studentIdPanel) studentIdPanel.SetActive(true);
            if (nextClassPanel) nextClassPanel.SetActive(false);
            SetText(studentIdErrorText, string.Empty);
            if (studentIdInput) studentIdInput.text = StudentId ?? string.Empty;
        }

        void ShowNextClassPanel()
        {
            if (studentIdPanel) studentIdPanel.SetActive(false);
            if (nextClassPanel) nextClassPanel.SetActive(true);
        }

        void OnConfirmStudentId()
        {
            var id = studentIdInput ? studentIdInput.text.Trim() : string.Empty;
            if (!IsValidStudentId(id))
            {
                SetText(studentIdErrorText, "Digits only, please.");
                return;
            }
            SetStudentId(id);
        }

        /// <summary>Sets and remembers the student ID, then downloads the timetable.</summary>
        public void SetStudentId(string id)
        {
            if (id != StudentId) events.Clear();
            StudentId = id;
            PlayerPrefs.SetString(StudentIdPrefKey, id);
            PlayerPrefs.Save();
            ShowNextClassPanel();
            Refresh();
        }

        static bool IsValidStudentId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 20) return false;
            foreach (var c in id)
                if (!char.IsDigit(c)) return false;
            return true;
        }

        // ---------- Loading ----------

        public void Refresh()
        {
            if (isLoading || string.IsNullOrEmpty(StudentId)) return;
            isLoading = true;
            if (events.Count == 0) ShowPlaceholder("Loading…", string.Empty);

            StartCoroutine(TimetableClient.FetchEvents(StudentId, OnEventsLoaded, OnLoadError,
                daysAhead, projectId, baseUrl));
        }

        void OnEventsLoaded(List<ClassEvent> loaded)
        {
            events.Clear();
            events.AddRange(loaded);
            FinishLoading();
            cardDirty = true;
            UpdateNextClass();
        }

        void OnLoadError(string message)
        {
            Debug.LogWarning("[ClassFinder] " + message);
            FinishLoading();
            if (events.Count == 0) ShowPlaceholder("Timetable unavailable", message);
        }

        void FinishLoading()
        {
            isLoading = false;
            nextRefreshTime = autoRefreshMinutes > 0 ? Time.unscaledTime + autoRefreshMinutes * 60f : float.MaxValue;
        }

        // ---------- Display ----------

        void UpdateNextClass()
        {
            if (isLoading && events.Count == 0) return;

            var nowUtc = DateTime.UtcNow;
            var next = ICalParser.FindNext(events, nowUtc);

            if (cardDirty || !ReferenceEquals(next, NextClass))
            {
                cardDirty = false;
                NextClass = next;
                RenderCard(next);
                NextClassChanged?.Invoke(next);
            }

            if (next != null)
            {
                SetText(headerText, next.IsOngoing(nowUtc) ? "NOW" : "NEXT CLASS");
                SetText(countdownText, FormatCountdown(next, nowUtc));
            }
        }

        void RenderCard(ClassEvent c)
        {
            if (c == null)
            {
                if (!isLoading && !string.IsNullOrEmpty(StudentId))
                    ShowPlaceholder("No upcoming class", $"Nothing in the next {daysAhead} days.");
                if (goToNextClassButton) goToNextClassButton.interactable = false;
                return;
            }

            SetText(titleText, string.IsNullOrWhiteSpace(c.Summary) ? "Class" : c.Summary);
            SetText(roomText, c.HasLocation ? c.Location : "Room not specified");
            SetText(timeText, FormatTime(c));
            if (goToNextClassButton) goToNextClassButton.interactable = true;
        }

        void ShowPlaceholder(string title, string info)
        {
            SetText(headerText, "NEXT CLASS");
            SetText(titleText, title);
            SetText(roomText, string.Empty);
            SetText(timeText, info);
            SetText(countdownText, string.Empty);
            if (goToNextClassButton) goToNextClassButton.interactable = false;
        }

        static string FormatTime(ClassEvent c)
        {
            var start = c.StartLocal;
            var today = DateTime.Now.Date;
            string day = start.Date == today ? "Today"
                : start.Date == today.AddDays(1) ? "Tomorrow"
                : start.ToString("ddd d MMM", DisplayCulture);
            return $"{day} · {start.ToString("HH:mm", DisplayCulture)} – {c.EndLocal.ToString("HH:mm", DisplayCulture)}";
        }

        static string FormatCountdown(ClassEvent c, DateTime nowUtc)
        {
            if (c.IsOngoing(nowUtc))
                return $"Started {FormatSpan(nowUtc - c.StartUtc)} ago";
            return $"Starts in {FormatSpan(c.StartUtc - nowUtc)}";
        }

        static string FormatSpan(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return "less than a minute";
            var sb = new StringBuilder();
            if (t.Days > 0) sb.Append(t.Days).Append(" d ");
            if (t.Hours > 0) sb.Append(t.Hours).Append(" h ");
            if (t.Days == 0) sb.Append(t.Minutes).Append(" min");
            return sb.ToString().Trim();
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label) label.text = value;
        }
    }
}
