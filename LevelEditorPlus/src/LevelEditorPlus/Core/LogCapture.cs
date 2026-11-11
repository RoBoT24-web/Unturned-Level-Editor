using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelEditorPlus.Core
{
    public class LogEntry
    {
        public string Message;
        public string StackTrace;
        public LogType Type;
        public DateTime Time;
    }

    /// <summary>
    /// Captures Unity log output (which includes UnturnedLog) into a ring
    /// buffer the console window can display.
    /// </summary>
    public static class LogCapture
    {
        public const int MaxEntries = 1000;

        private static readonly object gate = new object();
        private static readonly List<LogEntry> entries = new List<LogEntry>(MaxEntries);

        /// <summary>Incremented on every change; lets the UI cheaply detect updates.</summary>
        public static int Revision { get; private set; }

        public static void Begin()
        {
            // Threaded variant also catches logs from worker threads.
            Application.logMessageReceivedThreaded += Handle;
        }

        public static void End()
        {
            Application.logMessageReceivedThreaded -= Handle;
        }

        private static void Handle(string condition, string stackTrace, LogType type)
        {
            lock (gate)
            {
                if (entries.Count >= MaxEntries)
                {
                    entries.RemoveRange(0, entries.Count - MaxEntries + 1);
                }
                entries.Add(new LogEntry
                {
                    Message = condition,
                    StackTrace = stackTrace,
                    Type = type,
                    Time = DateTime.Now,
                });
                Revision++;
            }
        }

        public static void Clear()
        {
            lock (gate)
            {
                entries.Clear();
                Revision++;
            }
        }

        /// <summary>Copies entries matching the filter into the given list.</summary>
        public static void Snapshot(List<LogEntry> into, string filter)
        {
            into.Clear();
            lock (gate)
            {
                foreach (LogEntry e in entries)
                {
                    if (string.IsNullOrEmpty(filter) ||
                        e.Message.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        into.Add(e);
                    }
                }
            }
        }
    }
}
