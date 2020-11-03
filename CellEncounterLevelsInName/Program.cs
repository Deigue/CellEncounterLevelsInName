using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;

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

        public static void RunPatch(SynthesisState<ISkyrimMod, ISkyrimModGetter> state)
        {
            bool debugMode = true;

            Console.WriteLine("Running Cell Encounter Levels In Name ...");
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

                if (debugMode) Console.WriteLine($"Cell {cellName} with ECZN {minLevel} to {maxLevel}.");
                cellCounter++;
            }


            Console.WriteLine($"Patched {cellCounter} Cells.");
        }
    }
}
