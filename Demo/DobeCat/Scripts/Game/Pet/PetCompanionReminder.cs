using System;
using System.Collections.Generic;
using Demo.DobeCat.Sys;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// Companion reminder system — DESIGN.md §7.
    /// Tracks user activity (Win32 GetLastInputInfo on Windows) and fires
    /// speech-bubble reminders for sit-break, water, hourly chime, late-night,
    /// and focus encouragement.
    /// </summary>
    public class PetCompanionReminder : MonoBehaviour
    {
        public static PetCompanionReminder Instance { get; private set; }
        [Header("Thresholds (seconds)")]
        [Tooltip("Idle threshold: user is considered 'active' when idle < this.")]
#pragma warning disable CS0414
        [SerializeField] private float _idleThreshold     = 120f;   // 2 min idle = not active (used under #if WIN)
#pragma warning restore CS0414

        [Tooltip("Continuous active time before sit-break reminder (default 45 min).")]
        [SerializeField] private float _sitBreakThreshold = 2700f;  // 45 min

        [Tooltip("Interval between water reminders (default 60 min).")]
        [SerializeField] private float _waterInterval     = 3600f;  // 60 min

        [Tooltip("Continuous active time before focus encouragement (default 30 min).")]
        [SerializeField] private float _focusThreshold    = 1800f;  // 30 min

        // ─── State ────────────────────────────────────────────────────────────
        private float _activeAccum;          // continuous active seconds
        private float _waterTimer;           // counts up to _waterInterval
        private int   _lastChimeHour  = -1;  // last hour hourly chime fired
        private int   _lateNightDay   = -1;  // day-of-year late-night reminder fired
        private bool  _sitBreakFired;        // prevent multiple sit-break triggers per session
        private bool  _focusFired;           // prevent multiple focus triggers per session

        // ─── Pomodoro ─────────────────────────────────────────────────────────
        private enum PomodoroPhase { None, Focus, Break }
        private PomodoroPhase _pomPhase   = PomodoroPhase.None;
        private float         _pomTimer   = 0f;
        private float         _pomFocusSec;
        private float         _pomBreakSec;

        public bool PomodoroActive => _pomPhase != PomodoroPhase.None;

        // ─── 自定义闹钟 ────────────────────────────────────────────────────
        private struct AlarmEntry
        {
            public int    Hour;
            public int    Minute;
            public string Label;
            public int    LastFiredDay; // System.DateTime.Now.DayOfYear
        }

        private readonly List<AlarmEntry> _alarms = new List<AlarmEntry>();

        /// <summary>添加一个每日闹钟。已存在相同时刻则更新标签。</summary>
        public void AddAlarm(int hour, int minute, string label = "")
        {
            for (int i = 0; i < _alarms.Count; i++)
            {
                if (_alarms[i].Hour == hour && _alarms[i].Minute == minute)
                {
                    var e = _alarms[i]; e.Label = label; _alarms[i] = e;
                    Debug.Log($"[PetCompanionReminder] 闹钟更新: {hour:D2}:{minute:D2} {label}");
                    return;
                }
            }
            _alarms.Add(new AlarmEntry { Hour = hour, Minute = minute, Label = label, LastFiredDay = -1 });
            Debug.Log($"[PetCompanionReminder] 闹钟添加: {hour:D2}:{minute:D2} {label}");
        }

        /// <summary>从 "HH:mm" 或 "HH:mm 标签" 格式字符串添加闹钟。</summary>
        public bool AddAlarmFromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var parts = input.Trim().Split(new char[]{' '}, 2);
            var timePart = parts[0];
            var label = parts.Length > 1 ? parts[1].Trim() : "闹钟提醒";
            var timeParts = timePart.Split(':');
            if (timeParts.Length != 2) return false;
            if (!int.TryParse(timeParts[0], out var h) || !int.TryParse(timeParts[1], out var m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            AddAlarm(h, m, label);
            return true;
        }

        /// <summary>返回当前闹钟列表的可读字符串。</summary>
        public string GetAlarmsDisplay()
        {
            if (_alarms.Count == 0) return "无闹钟";
            var sb = new System.Text.StringBuilder();
            foreach (var a in _alarms)
                sb.Append($"{a.Hour:D2}:{a.Minute:D2} {a.Label}  ");
            return sb.ToString().TrimEnd();
        }

        /// <summary>清除所有闹钟。</summary>
        public void ClearAlarms()
        {
            _alarms.Clear();
            Debug.Log("[PetCompanionReminder] 所有闹钟已清除");
        }

        /// <summary>Start a Pomodoro cycle. Default 25 min focus / 5 min break.</summary>
        public void StartPomodoro(int focusMinutes = 25, int breakMinutes = 5)
        {
            _pomFocusSec = focusMinutes * 60f;
            _pomBreakSec = breakMinutes * 60f;
            _pomTimer    = _pomFocusSec;
            _pomPhase    = PomodoroPhase.Focus;
            ShowBubble($"番茄钟启动！专注 {focusMinutes} 分钟 🍅");
        }

        /// <summary>Cancel a running Pomodoro.</summary>
        public void StopPomodoro()
        {
            if (_pomPhase == PomodoroPhase.None) return;
            _pomPhase = PomodoroPhase.None;
            ShowBubble("番茄钟已取消。");
        }

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var dt      = Time.unscaledDeltaTime;
            var isActive = IsUserActive();

            if (isActive)
            {
                _activeAccum += dt;
                _waterTimer  += dt;
            }
            else
            {
                // Reset active counters when user goes idle (> threshold)
                if (_activeAccum > 0f)
                {
                    _activeAccum  = 0f;
                    _sitBreakFired = false;
                    _focusFired    = false;
                }
            }

            CheckSitBreak();
            CheckFocus();
            CheckWater();
            CheckHourlyChime();
            CheckLateNight();
            CheckPomodoro(dt);
            CheckAlarms();
        }

        // ─── Checks ───────────────────────────────────────────────────────────

        private void CheckSitBreak()
        {
            if (_sitBreakFired || _activeAccum < _sitBreakThreshold) return;
            _sitBreakFired = true;
            ShowBubble("该起来活动一下了！久坐伤身～");
        }

        private void CheckFocus()
        {
            if (_focusFired || _activeAccum < _focusThreshold) return;
            _focusFired = true;
            ShowBubble("你已经专注了很长时间，好棒！继续加油！");
        }

        private void CheckWater()
        {
            if (_waterTimer < _waterInterval) return;
            _waterTimer -= _waterInterval;
            ShowBubble("记得喝水哦～保持水分很重要！");
        }

        private void CheckHourlyChime()
        {
            var now = System.DateTime.Now;
            if (now.Minute != 0 || now.Second > 10) return;       // only at :00 within 10s
            if (_lastChimeHour == now.Hour) return;                // already chimed this hour
            _lastChimeHour = now.Hour;
            ShowBubble($"现在是 {now:HH:00}，注意时间哦～");
        }

        private void CheckLateNight()
        {
            var now = System.DateTime.Now;
            if (now.Hour < 23) return;
            var today = now.DayOfYear;
            if (_lateNightDay == today) return;
            _lateNightDay = today;
            ShowBubble("主人，已经很晚了，早点休息哦～");
        }

        private void CheckPomodoro(float dt)
        {
            if (_pomPhase == PomodoroPhase.None) return;
            _pomTimer -= dt;
            if (_pomTimer > 0f) return;

            if (_pomPhase == PomodoroPhase.Focus)
            {
                _pomPhase = PomodoroPhase.Break;
                _pomTimer = _pomBreakSec;
                var breakMin = Mathf.RoundToInt(_pomBreakSec / 60f);
                ShowBubble($"🍅 番茄钟结束！好好休息 {breakMin} 分钟吧～");
            }
            else
            {
                _pomPhase = PomodoroPhase.None;
                ShowBubble("⏰ 休息结束！继续加油！");
            }
        }

        private void CheckAlarms()
        {
            if (_alarms.Count == 0) return;
            var now = System.DateTime.Now;
            if (now.Second > 30) return;
            var today = now.DayOfYear;
            for (int i = 0; i < _alarms.Count; i++)
            {
                var a = _alarms[i];
                if (a.LastFiredDay == today) continue;
                if (a.Hour != now.Hour || a.Minute != now.Minute) continue;
                a.LastFiredDay = today;
                _alarms[i] = a;
                var label = string.IsNullOrEmpty(a.Label) ? "闹钟提醒" : a.Label;
                ShowBubble($"⏰ {a.Hour:D2}:{a.Minute:D2} {label}");
                PetSoundController.PlayNotify();
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static void ShowBubble(string msg)
        {
            if (PetSpeechBubble.Instance != null)
                PetSpeechBubble.Instance.Show(msg, 6f);
        }

        /// <summary>
        /// Returns true when user keyboard/mouse has been active recently.
        /// Uses Win32 GetLastInputInfo on Windows builds; falls back to
        /// Unity Input polling in Editor/other platforms.
        /// </summary>
        private bool IsUserActive()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetIdleSeconds() < _idleThreshold;
#else
            return Input.anyKey || Input.GetAxis("Mouse X") != 0f || Input.GetAxis("Mouse Y") != 0f;
#endif
        }
    }
}
