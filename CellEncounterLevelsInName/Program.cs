using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace CellEncounterLevelsInName
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return SynthesisPipeline.Instance.Patch<ISkyrimMod, ISkyrimModGetter>(
                args: args,
                patcher: RunPatch,
                new UserPreferences()
                {
                    ActionsForEmptyArgs = new RunDefaultPatcher
                    {

                        IdentifyingModKey = "CellEncounterLevelsInName.esp",
                        TargetRelease = GameRelease.SkyrimSE
                    }
                }
            );
        }

        private static bool ParseTemplateString(JObject jObject, string keyName, out string parsedString)
        {
            parsedString = "ERROR"; // Default state.
            if (jObject.ContainsKey(keyName))
            {
                var parse = jObject[keyName]?
                .ToString()
                .Replace("{name}", "{0}")
                .Replace("{min}", "{1}")
                .Replace("{max}", "{2}");

                if (parse != null) parsedString = parse;
            }

            return parsedString != "ERROR";
        }

        public static void RunPatch(SynthesisState<ISkyrimMod, ISkyrimModGetter> state)
        {
            bool debugMode = true;
            Console.WriteLine(); // Spaces out this patchers output.

            string configFilePath = Path.Combine(state.ExtraSettingsDataPath, "config.json");
            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("\"config.json\" cannot be found in the users Data folder.");
                return;
            }

            JObject config = JObject.Parse(File.ReadAllText(configFilePath));
            string formulaRangedLeveled = "";
            string formulaDeleveled = "";
            string formulaLeveled = "";
            bool mapMarkers = false;

            if (!ParseTemplateString(config, "formulaRangedLeveled", out formulaRangedLeveled) ||
                !ParseTemplateString(config, "formulaDeleveled", out formulaDeleveled) ||
                !ParseTemplateString(config, "formulaLeveled", out formulaLeveled))
            {
                Console.WriteLine("Fields \"formulaRangedLeveled\", \"formulaDeleveled\" and \"formulaLeveled\" must be specified in \"config.json\"");
                return;
            }
            
            Console.WriteLine("*** Cell Encounter Levels In Name - Configuration ***");
            Console.WriteLine($" formulaRangedLeveled: {formulaRangedLeveled}");
            Console.WriteLine($" formulaDeleveled: {formulaDeleveled}");
            Console.WriteLine($" formulaLeveled: {formulaLeveled}");
            Console.WriteLine("Running Cell Encounter Levels In Name ...");
            Console.WriteLine();

            int cellCounter = 0;
            ILinkCache cache = state.LinkCache;
            foreach (var cellContext in state.LoadOrder.PriorityOrder.Cell().WinningContextOverrides(cache))
            {
                var cell = cellContext.Record;
                if (string.IsNullOrEmpty(cell.Name?.String) || cell.EncounterZone.FormKey == null || cellContext.IsUnderneath<IWorldspaceGetter>())
                {
                    continue;
                }
                cell.EncounterZone.TryResolve(cache, out var encounterZone);
                if (encounterZone == null) continue;

                string cellName = cell.Name.String;
                sbyte minLevel = encounterZone.MinLevel;
                sbyte maxLevel = encounterZone.MaxLevel;

                string nameTemplate;
                if (maxLevel > minLevel)
                {
                    nameTemplate = formulaRangedLeveled;
                }
                else if (maxLevel == minLevel)
                {
                    nameTemplate = formulaDeleveled;
                }
                else
                {
                    nameTemplate = formulaLeveled;
                }


                string newCellName = string.Format(nameTemplate, cellName, minLevel, maxLevel);

                if (debugMode) Console.WriteLine($"Changing Cell name from \"{cellName}\" to \"{newCellName}\"");

                var overriddenCell = cellContext.GetOrAddAsOverride(state.PatchMod);
                overriddenCell.Name = newCellName;
                cellCounter++;

                if (!mapMarkers) continue;

                cell.Location.TryResolve(state.LinkCache, out var location);
                if (location != null)
                {
                    //location.
                }
            }


            Console.WriteLine();
            Console.WriteLine($"Patched {cellCounter} Cells.");
            Console.WriteLine();
        }
    }
}
