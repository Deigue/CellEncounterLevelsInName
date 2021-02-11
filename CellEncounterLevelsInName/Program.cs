using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.IO;

namespace CellEncounterLevelsInName
{
    public static class Program
    {
        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args, new RunPreferences()
                {
                    ActionsForEmptyArgs = new RunDefaultPatcher
                    {
                        IdentifyingModKey = "CellEncounterLevelsInName.esp",
                        TargetRelease = GameRelease.SkyrimSE
                    }
                });
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


        private static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            bool debugMode = false; // more debugging messages.
            bool changeMapMarkers = false;

            Console.WriteLine(); // Spaces out this patchers output.

            string configFilePath = Path.Combine(state.ExtraSettingsDataPath, "config.json");

            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("\"config.json\" cannot be found in the users Data folder.");
                return;
            }

            JObject config = JObject.Parse(File.ReadAllText(configFilePath));

            if (config.TryGetValue("patchMapMarkers", out var jToken))
                changeMapMarkers = jToken.Value<bool?>() ?? false;

            if (!ParseTemplateString(config, "formulaRangedLeveled", out string formulaRangedLeveled) ||
                !ParseTemplateString(config, "formulaDeleveled", out string formulaDeleveled) ||
                !ParseTemplateString(config, "formulaLeveled", out string formulaLeveled))
            {
                Console.WriteLine(
                    "Fields \"formulaRangedLeveled\", \"formulaDeleveled\" and \"formulaLeveled\" must be specified in \"config.json\"");
                return;
            }

            Config configuration = new Config(formulaRangedLeveled, formulaDeleveled, formulaLeveled);

            Console.WriteLine("*** Cell Encounter Levels In Name - Configuration ***");
            Console.WriteLine($" formulaRangedLeveled: {configuration.FormulaRangedLeveled}");
            Console.WriteLine($" formulaDeleveled: {configuration.FormulaDeleveled}");
            Console.WriteLine($" formulaLeveled: {configuration.FormulaLeveled}");
            Console.WriteLine($" patchMapMarkers: {changeMapMarkers}");
            Console.WriteLine("Running Cell Encounter Levels In Name ...");
            Console.WriteLine("*****************************************************");
            Console.WriteLine();

            var cellCounter = 0;
            var mapMarkerCounter = 0;
            ILinkCache cache = state.LinkCache;
            var markerContexts =
                new Lazy<Dictionary<FormKey, IModContext<ISkyrimMod, IPlacedObject, IPlacedObjectGetter>>>();
            var mapMarkerZones = new Lazy<Dictionary<IPlacedObjectGetter, HashSet<IEncounterZoneGetter>>>(() =>
                new Dictionary<IPlacedObjectGetter, HashSet<IEncounterZoneGetter>>(MajorRecord
                    .FormKeyEqualityComparer));

            if (changeMapMarkers)
            {
                Console.WriteLine("Loading all winning map markers for changing later ...");
                state.LoadOrder.PriorityOrder.PlacedObject().WinningContextOverrides(cache)
                    .Where(ctx => !string.IsNullOrEmpty(ctx.Record.MapMarker?.Name?.String))
                    .ForEach(ctx => markerContexts.Value.Add(ctx.Record.FormKey, ctx));
            }

            Console.WriteLine("Starting cell overrides ...");
            foreach (var cellContext in state.LoadOrder.PriorityOrder.Cell().WinningContextOverrides(cache))
            {
                var cell = cellContext.Record;
                if (string.IsNullOrEmpty(cell.Name?.String) || cell.EncounterZone.FormKeyNullable is null ||
                    cellContext.IsUnderneath<IWorldspaceGetter>())
                {
                    continue;
                }

                cell.EncounterZone.TryResolve(cache, out var encounterZone);
                if (encounterZone is null) continue;

                string cellName = cell.Name.String;
                sbyte minLevel = encounterZone.MinLevel;
                sbyte maxLevel = encounterZone.MaxLevel;

                var newCellName = configuration.MakeNewName(cellName, minLevel, maxLevel);

                Console.WriteLine($"Changing Cell name from \"{cellName}\" to \"{newCellName}\"");

                var overriddenCell = cellContext.GetOrAddAsOverride(state.PatchMod);
                overriddenCell.Name = newCellName;
                cellCounter++;

                if (!changeMapMarkers) continue;

                cell.Location.TryResolve(cache, out var location);
                if (location is null) continue;
                FormKey markerFormKey = location.WorldLocationMarkerRef.FormKey;
                if (markerFormKey == FormKey.Null ||
                    !markerContexts.Value.TryGetValue(markerFormKey, out var placedContext)) continue;

                var placedObject = placedContext.Record;
                if (!mapMarkerZones.Value.ContainsKey(placedObject))
                {
                    var encounterZones = new HashSet<IEncounterZoneGetter>(MajorRecord.FormKeyEqualityComparer)
                        {encounterZone};
                    mapMarkerZones.Value.Add(placedObject, encounterZones);

                    if (debugMode) Console.WriteLine($">> New MapMarkerZone for {placedObject.MapMarker?.Name}.");
                }
                else if (mapMarkerZones.Value.TryGetValue(placedObject, out var encounterZones))
                {
                    encounterZones.Add(encounterZone);
                    if (debugMode)
                        Console.WriteLine($">>>> New ECZN {encounterZone.FormKey} for {placedObject.MapMarker?.Name}.");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Patched {cellCounter} Cells.");
            Console.WriteLine();


            if (mapMarkerZones.IsValueCreated) // Implies activity occurred in populating map marker zones ...
            {
                Console.WriteLine("Patching Map Marker Names ...");
                foreach (var (placedObject, encounterZoneSet) in mapMarkerZones.Value)
                {
                    var mapMarkerName = placedObject.MapMarker?.Name?.String;
                    if (mapMarkerName is null) continue;
                    if (!markerContexts.Value.TryGetValue(placedObject.FormKey, out var matchingContext)) continue;

                    sbyte minLevel = 127;
                    sbyte maxLevel = -128;
                    foreach (var encounterZone in encounterZoneSet)
                    {
                        minLevel = Math.Min(minLevel, encounterZone.MinLevel);
                        maxLevel = Math.Max(maxLevel, encounterZone.MaxLevel);
                    }

                    var newMarkerName = configuration.MakeNewName(mapMarkerName, minLevel, maxLevel);

                    Console.WriteLine($"Changing Map marker from \"{mapMarkerName}\" to \"{newMarkerName}\"");

                    IPlacedObject newPlacedObject = matchingContext.GetOrAddAsOverride(state.PatchMod);
                    
                    if (newPlacedObject.MapMarker is null) continue; // Should never happen, but doesn't hurt.
                    newPlacedObject.MapMarker.Name = newMarkerName;
                    mapMarkerCounter++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Patched {mapMarkerCounter} Map markers.");
            Console.WriteLine();
        }
    }
}