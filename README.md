# Cell Encounter Levels In Name

Finds the Encounter Zone minimum and maximum levels and patches them into the cell names based on the template specified. Corresponding Map Markers will also be patched to reflect the changes (this can be toggled off if needed)

Configuration file (Data/config.json)

- formulaRangedLeveled : Used when encounter zone max level > min level.
- formulaDeleveled : Used when min and max level of the encounter zone is the same.
- formulaLeveled : Used when minimum level is more than max level. This usually happens when max level is unspecified (0)
- patchMapMarkers : Indicate if Map Markers should be patched as well. (true/false)

Default Configuration file:
```
{
    "formulaRangedLeveled" : "{name} ({min} ~ {max})",
    "formulaDeleveled": "{name} ({min})",
    "formulaLeveled": "{name} ({min}+)",
    "patchMapMarkers": true
}
```
