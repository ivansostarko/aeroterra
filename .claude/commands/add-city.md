Add a new Free Flight city: $ARGUMENTS

Steps:
1. Open `Assets/Scripts/Core/GameManager.cs` and find `MapDefinition.All`.
2. Add one new `MapDefinition` entry with the city name and real-world latitude/longitude/spawn altitude (look up real coordinates, don't guess).
3. Do not touch any UI code — `FreeFlightMenuUI.cs` builds its map list from `MapDefinition.All` automatically.
4. Confirm the new entry follows the existing pattern (see London/Dubai entries) and keeps the file's brace balance intact.
