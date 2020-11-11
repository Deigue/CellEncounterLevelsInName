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
using System.Runtime.InteropServices.ComTypes;

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
            bool debugMode = true; // more debugging messages.
            //bool changeMapMarkers = false; // make this configurable later.
            bool changeMapMarkers = true;

            Console.WriteLine(); // Spaces out this patchers output.

            string formulaRangedLeveled = "";
            string formulaDeleveled = "";
            string formulaLeveled = "";
            string configFilePath = Path.Combine(state.ExtraSettingsDataPath, "config.json");

            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("\"config.json\" cannot be found in the users Data folder.");
                return;
            }

            JObject config = JObject.Parse(File.ReadAllText(configFilePath));
            
            

            if (!ParseTemplateString(config, "formulaRangedLeveled", out formulaRangedLeveled) ||
                !ParseTemplateString(config, "formulaDeleveled", out formulaDeleveled) ||
                !ParseTemplateString(config, "formulaLeveled", out formulaLeveled))
            {
                Console.WriteLine("Fields \"formulaRangedLeveled\", \"formulaDeleveled\" and \"formulaLeveled\" must be specified in \"config.json\"");
                return;
            }

            Config configuration = new Config(formulaRangedLeveled, formulaDeleveled, formulaLeveled);

            Console.WriteLine("*** Cell Encounter Levels In Name - Configuration ***");
            Console.WriteLine($" formulaRangedLeveled: {configuration.FormulaRangedLeveled}");
            Console.WriteLine($" formulaDeleveled: {configuration.FormulaDeleveled}");
            Console.WriteLine($" formulaLeveled: {configuration.FormulaLeveled}");
            Console.WriteLine("Running Cell Encounter Levels In Name ...");
            Console.WriteLine("*****************************************************");
            Console.WriteLine();

            int cellCounter = 0;
            int mapMarkerCounter = 0;
            ILinkCache cache = state.LinkCache;
            var markerContexts = new Lazy<Dictionary<FormKey, ModContext<ISkyrimMod, IPlacedObject, IPlacedObjectGetter>>>();
            var mapMarkerZones = new Lazy<Dictionary<IPlacedObjectGetter, HashSet<IEncounterZoneGetter>>>(() =>
                new Dictionary<IPlacedObjectGetter, HashSet<IEncounterZoneGetter>>(MajorRecord.FormKeyEqualityComparer));

            Console.WriteLine($"Loading all winning map markers for changing later ...");
            if (changeMapMarkers)
            {
                state.LoadOrder.PriorityOrder.PlacedObject().WinningContextOverrides(cache)
                    .Where(ctx => !string.IsNullOrEmpty(ctx.Record.MapMarker?.Name?.String))
                    .ForEach(ctx => markerContexts.Value.Add(ctx.Record.FormKey, ctx));
            }

            Console.WriteLine("Starting cell overrides ...");
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

                var newCellName = configuration.MakeNewName(cellName, minLevel, maxLevel);
               
                //Console.WriteLine($"Changing Cell name from \"{cellName}\" to \"{newCellName}\"");

                var overriddenCell = cellContext.GetOrAddAsOverride(state.PatchMod);
                overriddenCell.Name = newCellName;
                cellCounter++;

                if (!changeMapMarkers) continue;

                cell.Location.TryResolve(cache, out var location);
                if (location == null) continue;
                FormKey markerFormKey = location.WorldLocationMarkerRef.FormKey ?? FormKey.Null;
                if (markerFormKey == FormKey.Null || !markerContexts.Value.TryGetValue(markerFormKey, out var placedContext) || placedContext == null) continue;

                var placedObject = placedContext.Record;
                var mapMarker = placedObject.MapMarker;
                if (!mapMarkerZones.Value.ContainsKey(placedObject))
                {
                    var encounterZones = new HashSet<IEncounterZoneGetter>(MajorRecord.FormKeyEqualityComparer) {encounterZone};
                    mapMarkerZones.Value.Add(placedObject, encounterZones);

                    if (debugMode) Console.WriteLine($">> New MapMarkerZone for {placedObject.MapMarker?.Name}.");
                }
                else if (mapMarkerZones.Value.TryGetValue(placedObject, out var encounterZones))
                {
                    encounterZones.Add(encounterZone);
                    if (debugMode) Console.WriteLine($">>>> New ECZN {encounterZone.FormKey} for {placedObject.MapMarker?.Name}.");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Patched {cellCounter} Cells.");
            Console.WriteLine();
            

            if (mapMarkerZones.IsValueCreated) // Implies activity occurred in populating map marker zones ...
            {
                Console.WriteLine($"Patching Map Marker Names ...");
                foreach (var mapMarkerZone in mapMarkerZones.Value)
                {
                    var placedObject = mapMarkerZone.Key;
                    if (placedObject == null) continue;
                    var mapMarkerName = placedObject.MapMarker?.Name?.String;
                    if (mapMarkerName == null) continue;
                    if (!markerContexts.Value.TryGetValue(placedObject.FormKey, out var matchingContext)) continue;

                    sbyte minLevel = 127;
                    sbyte maxLevel = -128;
                    foreach ( var encounterZone in mapMarkerZone.Value)
                    {
                        minLevel = Math.Min(minLevel, encounterZone.MinLevel);
                        maxLevel = Math.Max(maxLevel, encounterZone.MaxLevel);
                    }

                    var newMarkerName = configuration.MakeNewName(mapMarkerName, minLevel, maxLevel);


                    // contextual information, ready made map marker is here ... no info is known about its parent worldSpace.
                    //var modifiedMapMarker = placedObject.DeepCopy();
                    // var matchingContext = state.LoadOrder.PriorityOrder.
                    /*
                    var matchingContext = state.LoadOrder.PriorityOrder.PlacedObject().WinningContextOverrides(cache)
                        .First(ctx => ctx.Record.FormKey == placedObject.FormKey);
                    
                        .Where(context => context.Record is IPlacedObjectGetter)
                        .First(context =>
                        {
                            var placedObj = context.Record as IPlacedObjectGetter;
                            return (placedObj == placedObject);
                        });
                    */

                    Console.WriteLine($"Changing Map marker from \"{mapMarkerName}\" to \"{newMarkerName}\"");
                    var newPlacedObject =  matchingContext.GetOrAddAsOverride(state.PatchMod);

                    Console.WriteLine($"newPlacedObject: {newPlacedObject.FormKey}");
                    //var newPlacedObject = placedOverride as PlacedObject;
                    if (newPlacedObject == null || newPlacedObject.MapMarker == null) continue;
                    newPlacedObject.MapMarker.Name = newMarkerName;
                    mapMarkerCounter++;

                    
                    if (mapMarkerCounter == 5)
                    {
                        // good enuf sample size, stop here.
                        break;
                    }

                }
            }

            Console.WriteLine();
            Console.WriteLine($"Patched {mapMarkerCounter} Map markers.");
            Console.WriteLine();
        }
    }
}
