﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using FilePath = System.IO.Path;

namespace XCOM2Launcher.Mod
{
    public class ModEntry
    {
        [JsonIgnore] private string _image;

        [JsonIgnore] private IEnumerable<ModClassOverride> _overrides;

        /// <summary>
        ///     Index to determine mod load order
        /// </summary>
        [DefaultValue(-1)]
        public int Index { get; set; } = -1;

        [JsonIgnore]
        public ModState State { get; set; } = ModState.None;

        public string ID { get; set; }
        public string Name { get; set; }
        public bool ManualName { get; set; } = false;

        public string Author { get; set; } = "Unknown";

        public string Path { get; set; }

        /// <summary>
        ///     Size in bytes
        /// </summary>
        [DefaultValue(-1)]
        public long Size { get; set; } = -1;

        public bool isActive { get; set; } = false;
        public bool isHidden { get; set; } = false;

        public ModSource Source { get; set; } = ModSource.Unknown;

        [DefaultValue(-1)]
        public long WorkshopID { get; set; } = -1;

        public DateTime? DateAdded { get; set; } = null;
        public DateTime? DateCreated { get; set; } = null;
        public DateTime? DateUpdated { get; set; } = null;

        public string Note { get; set; } = null;

        [JsonIgnore]
        public string Image
        {
            get { return _image ?? FilePath.Combine(Path ?? "", "ModPreview.jpg"); }
            set { _image = value; }
        }

        public string GetDescription()
        {
            var info = new ModInfo(GetModInfoFile());

            return info.Description;
        }

        public IEnumerable<ModClassOverride> GetOverrides(bool forceUpdate = false)
        {
            if (_overrides == null || forceUpdate)
            {
                _overrides = GetUIScreenListenerOverrides().Union(GetClassOverrides()).ToList();
            }
            return _overrides;
        }

        private IEnumerable<ModClassOverride> GetUIScreenListenerOverrides()
        {
            var sourceDirectory = FilePath.Combine(Path, "Src");
            var overrides = new List<ModClassOverride>();

            if (!Directory.Exists(sourceDirectory))
            {
                return overrides;
            }

            var sourceFiles = Directory.GetFiles(sourceDirectory, "*.uc", SearchOption.AllDirectories);

            Parallel.ForEach(sourceFiles, sourceFile =>
            {
                //The XComGame directory usually contains ALL the source files for the game.  Leaving it in is a common mistake.
                if (sourceFile.IndexOf(@"Src\XComGame", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return;
                }

                var screenClassRegex = new Regex(@"(?i)^\s*ScreenClass\s*=\s*(?:class')?([a-z_]+)");

                foreach (var line in File.ReadLines(sourceFile))
                {
                    var match = screenClassRegex.Match(line);
                    if (match.Success)
                    {
                        var oldClass = match.Groups[1].Value;
                        if (oldClass.ToLower() == "none")
                        {
                            //'ScreenClass = none' means it runs against every UI screen
                            continue;
                        }

                        var newClass = FilePath.GetFileNameWithoutExtension(sourceFile);
                        lock (overrides)
                        {
                            overrides.Add(new ModClassOverride(this, newClass, oldClass, ModClassOverrideType.UIScreenListener));
                        }
                    }
                }
            });

            return overrides;
        }

        private IEnumerable<ModClassOverride> GetClassOverrides()
        {
            var file = FilePath.Combine(Path, "Config", "XComEngine.ini");

            if (!File.Exists(file))
                return new ModClassOverride[0];

            var r = new Regex("^[+]?ModClassOverrides=\\(BaseGameClass=\"([^\"]+)\",ModClass=\"([^\"]+)\"\\)");

            return from line in File.ReadLines(file)
                select r.Match(line.Replace(" ", ""))
                into m
                where m.Success
                select new ModClassOverride(this, m.Groups[2].Value, m.Groups[1].Value, ModClassOverrideType.Class);
        }

        public void ShowOnSteam()
        {
            Process.Start("explorer", "steam://url/CommunityFilePage/" + WorkshopID);
        }

        public void ShowInExplorer()
        {
            Process.Start("explorer", Path);
        }

        public string GetWorkshopLink()
        {
            return "https://steamcommunity.com/sharedfiles/filedetails/?id=" + WorkshopID;
        }

        public override string ToString()
        {
            return ID;
        }

        public bool IsInModPath(string modPath)
        {
            return 0 == string.Compare(modPath.TrimEnd('/', '\\'), FilePath.GetDirectoryName(Path), StringComparison.OrdinalIgnoreCase);
        }

        #region Files

        public string[] GetConfigFiles()
        {
            return Directory.GetFiles(FilePath.Combine(Path, "Config"), "*.ini");
        }

        internal string GetModInfoFile()
        {
            return FilePath.Combine(Path, ID + ".XComMod");
        }

        public string GetReadMe()
        {
            try
            {
                return File.ReadAllText(FilePath.Combine(Path, "ReadMe.txt"));
            }
            catch (IOException)
            {
                return "No ReadMe found.";
            }
        }

        #endregion
    }
}