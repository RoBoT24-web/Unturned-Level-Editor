using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SDG.Unturned;

namespace LevelEditorPlus.Features
{
    internal static class HierarchyExport
    {
        internal sealed class Entry
        {
            public string Name;
            public ushort Id;
            public Guid Guid;
            public string Category;
            public string Source;
            public bool IsBuildable;
            public bool MissingAsset;
        }

        public static List<Entry> Collect()
        {
            var entries = new List<Entry>();
            foreach (LevelObject obj in SelectionTools.EnumerateLevelObjects())
            {
                entries.Add(new Entry
                {
                    Name = obj.asset != null ? obj.asset.objectName : $"<missing asset> ({obj.id})",
                    Id = obj.id,
                    Guid = obj.GUID,
                    Category = obj.asset != null ? obj.asset.type.ToString() : "Object",
                    Source = obj.asset?.origin?.name ?? "unknown",
                    MissingAsset = obj.asset == null,
                });
            }
            foreach (LevelBuildableObject obj in SelectionTools.EnumerateBuildables())
            {
                entries.Add(new Entry
                {
                    Name = obj.asset != null ? obj.asset.itemName : $"<missing item> ({obj.id})",
                    Id = obj.id,
                    Guid = obj.asset?.GUID ?? Guid.Empty,
                    Category = obj.asset is ItemBarricadeAsset ? "Barricade" :
                        obj.asset is ItemStructureAsset ? "Structure" : "Buildable",
                    Source = obj.asset?.origin?.name ?? "unknown",
                    IsBuildable = true,
                    MissingAsset = obj.asset == null,
                });
            }
            entries.Sort((a, b) =>
            {
                int comparison = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (comparison == 0)
                    comparison = string.CompareOrdinal(a.Category, b.Category);
                if (comparison == 0)
                    comparison = a.Id.CompareTo(b.Id);
                return comparison != 0 ? comparison : a.Guid.CompareTo(b.Guid);
            });
            return entries;
        }

        public static int WriteDetailed(TextWriter writer, List<Entry> entries)
        {
            writer.WriteLine("Name,Asset ID,Asset GUID,Category,Asset Source");
            int missing = 0;
            foreach (Entry entry in entries)
            {
                writer.WriteLine(string.Join(",", CsvText(entry.Name),
                    entry.Id.ToString(CultureInfo.InvariantCulture),
                    entry.Guid == Guid.Empty ? string.Empty : entry.Guid.ToString("D"),
                    CsvText(entry.Category), CsvText(entry.Source)));
                if (entry.MissingAsset)
                    missing++;
            }
            return missing;
        }

        public static void WriteSummary(TextWriter writer, List<Entry> entries)
        {
            var quantities = new Dictionary<string, int>();
            var assets = new List<Entry>();
            foreach (Entry entry in entries)
            {
                string key = AssetKey(entry);
                if (quantities.TryGetValue(key, out int quantity))
                    quantities[key] = quantity + 1;
                else
                {
                    quantities.Add(key, 1);
                    assets.Add(entry);
                }
            }
            writer.WriteLine("Name,Asset ID,Category,Quantity");
            foreach (Entry entry in assets)
            {
                writer.WriteLine(string.Join(",", CsvText(entry.Name),
                    entry.Id.ToString(CultureInfo.InvariantCulture), CsvText(entry.Category),
                    quantities[AssetKey(entry)].ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static string AssetKey(Entry entry)
        {
            return (entry.IsBuildable ? "item:" : "object:") +
                (entry.Guid != Guid.Empty ? entry.Guid.ToString("D") :
                    entry.Id.ToString(CultureInfo.InvariantCulture));
        }

        private static string CsvText(string text)
        {
            // Quoting alone does not stop spreadsheet formula execution.
            if (!string.IsNullOrEmpty(text) && "=+-@\t\r\n".IndexOf(text[0]) >= 0)
                text = "'" + text;
            return "\"" + (text ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
