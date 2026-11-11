using System.Collections.Generic;
using System.Text;
using LevelEditorPlus.Core;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    /// <summary>
    /// In-editor console: shows captured game logs with search, copy, and clear.
    /// </summary>
    public class ConsoleWindow : DraggableWindow
    {
        private readonly ISleekField searchField;
        private readonly ISleekButton copyButton;
        private readonly ISleekButton clearButton;
        private readonly ISleekToggle autoScrollToggle;
        private readonly SleekList<LogEntry> list;

        private readonly List<LogEntry> rows = new List<LogEntry>();
        private string filter = string.Empty;
        private int lastRevision = -1;
        private float lastRebuild;

        public ConsoleWindow(WindowManager manager)
            : base(manager, "console", "Console", 40f, 540f, 760f, 280f, defaultVisible: true)
        {
            searchField = Glazier.Get().CreateStringField();
            searchField.PositionOffset_X = 4f;
            searchField.PositionOffset_Y = 4f;
            searchField.SizeScale_X = 1f;
            searchField.SizeOffset_X = -268f;
            searchField.SizeOffset_Y = 24f;
            searchField.PlaceholderText = "Search logs...";
            searchField.OnTextChanged += OnSearchChanged;
            content.AddChild(searchField);

            copyButton = Glazier.Get().CreateButton();
            copyButton.PositionScale_X = 1f;
            copyButton.PositionOffset_X = -260f;
            copyButton.PositionOffset_Y = 4f;
            copyButton.SizeOffset_X = 70f;
            copyButton.SizeOffset_Y = 24f;
            copyButton.Text = "Copy";
            copyButton.TooltipText = "Copy the visible log lines to the clipboard";
            copyButton.OnClicked += OnClickedCopy;
            content.AddChild(copyButton);

            clearButton = Glazier.Get().CreateButton();
            clearButton.PositionScale_X = 1f;
            clearButton.PositionOffset_X = -186f;
            clearButton.PositionOffset_Y = 4f;
            clearButton.SizeOffset_X = 70f;
            clearButton.SizeOffset_Y = 24f;
            clearButton.Text = "Clear";
            clearButton.OnClicked += OnClickedClear;
            content.AddChild(clearButton);

            autoScrollToggle = Glazier.Get().CreateToggle();
            autoScrollToggle.PositionScale_X = 1f;
            autoScrollToggle.PositionOffset_X = -112f;
            autoScrollToggle.PositionOffset_Y = 4f;
            autoScrollToggle.SizeOffset_X = 24f;
            autoScrollToggle.SizeOffset_Y = 24f;
            autoScrollToggle.Value = true;
            autoScrollToggle.AddLabel("Auto-scroll", ESleekSide.RIGHT);
            content.AddChild(autoScrollToggle);

            list = new SleekList<LogEntry>();
            list.PositionOffset_X = 4f;
            list.PositionOffset_Y = 32f;
            list.SizeScale_X = 1f;
            list.SizeScale_Y = 1f;
            list.SizeOffset_X = -8f;
            list.SizeOffset_Y = -36f;
            list.itemHeight = 22;
            list.itemPadding = 1;
            list.onCreateElement = CreateRow;
            list.SetData(rows);
            content.AddChild(list);
        }

        private ISleekElement CreateRow(LogEntry entry)
        {
            ISleekBox box = Glazier.Get().CreateBox();
            box.SizeScale_X = 1f;
            box.Text = $"[{entry.Time:HH:mm:ss}] {FirstLine(entry.Message)}";
            box.TextAlignment = TextAnchor.MiddleLeft;
            box.TextColor = ColorFor(entry.Type);
            box.TooltipText = string.IsNullOrEmpty(entry.StackTrace)
                ? entry.Message
                : entry.Message + "\n" + entry.StackTrace;
            return box;
        }

        private static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }
            int newline = message.IndexOf('\n');
            return newline >= 0 ? message.Substring(0, newline) : message;
        }

        private static SleekColor ColorFor(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return new SleekColor(new Color(1f, 0.45f, 0.45f));
                case LogType.Warning:
                    return new SleekColor(new Color(1f, 0.9f, 0.4f));
                default:
                    return new SleekColor(ESleekTint.FONT);
            }
        }

        private void OnSearchChanged(ISleekField field, string text)
        {
            filter = text;
            lastRevision = -1; // force rebuild
        }

        private void OnClickedCopy(ISleekElement button)
        {
            var sb = new StringBuilder();
            foreach (LogEntry e in rows)
            {
                sb.Append('[').Append(e.Time.ToString("HH:mm:ss")).Append("] ").AppendLine(e.Message);
            }
            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        private void OnClickedClear(ISleekElement button)
        {
            LogCapture.Clear();
        }

        protected override void OnTick()
        {
            if (lastRevision == LogCapture.Revision || Time.realtimeSinceStartup - lastRebuild < 0.25f)
            {
                return;
            }
            lastRevision = LogCapture.Revision;
            lastRebuild = Time.realtimeSinceStartup;

            LogCapture.Snapshot(rows, filter);
            list.NotifyDataChanged();
            if (autoScrollToggle.Value)
            {
                list.scrollView.ScrollToBottom();
            }
        }
    }
}
