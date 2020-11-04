using System;
using System.Collections.Generic;
using System.Text;

namespace CellEncounterLevelsInName
{
    public class Config
    {
        public string FormulaRangedLeveled { get; set; }
        public string FormulaDeleveled { get; set; }
        public string FormulaLeveled { get; set; }

        public Config(string formulaRangedLeveled, string formulaDeleved, string formulaLeveled)
        {
            FormulaRangedLeveled = formulaRangedLeveled;
            FormulaDeleveled = formulaDeleved;
            FormulaLeveled = formulaLeveled;
        }

        public string MakeNewName(string oldName, sbyte minLevel, sbyte maxLevel)
        {
            string nameTemplate;
            if (maxLevel > minLevel)
            {
                nameTemplate = FormulaRangedLeveled;
            }
            else if (maxLevel == minLevel)
            {
                nameTemplate = FormulaDeleveled;
            }
            else
            {
                nameTemplate = FormulaLeveled;
            }

            var newName = string.Format(nameTemplate, oldName, minLevel, maxLevel);
            return newName;
        }
    }
}
