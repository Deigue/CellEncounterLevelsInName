#Cell Encounter Levels In Name

Finds the Encounter Zone minimum and maximum levels and patches them into the cell names based on the template specified. Additional support will be added in future to also propagate this to map markers.

Configuration file (Data/config.json)

- formulaRangedLeveled : Used when encounter zone max level > min level.
- formulaDeleveled : Used when min and max level of the encounter zone is the same.
- formulaLeveled : Used when minimum level is more than max level. This usually happens when max level is unspecified (0)

Default Configuration file:
```
{
    "formulaRangedLeveled" : "{name} ({min} ~ {max})",
    "formulaDeleveled": "{name} ({min})",
    "formulaLeveled": "{name} ({min}+)"
}
```