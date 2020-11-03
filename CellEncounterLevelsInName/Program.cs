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
                    nameTemplate = "{0} ({1} ~ {2})";
                } 
                else if (maxLevel == minLevel) 
                {
                    nameTemplate = "{0} ({1})";
                }
                else
                {
                    nameTemplate = "{0} ({1}+)";
                }

                string newCellName = string.Format(nameTemplate, cellName, minLevel, maxLevel);

                if (debugMode) Console.WriteLine($"Changing Cell name from \"{cellName}\" to \"{newCellName}\"");

                var overriddenCell = cellContext.GetOrAddAsOverride(state.PatchMod);
                overriddenCell.Name = newCellName;
                cellCounter++;
            }

            Console.WriteLine();
            Console.WriteLine($"Patched {cellCounter} Cells.");
            Console.WriteLine();
        }
    }
}
