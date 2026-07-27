Build the Windows (x64) player:

```bash
./scripts/build-windows.sh
```

If `$UNITY` isn't set and no editor is auto-detected under `~/Unity/Hub/Editor/*`, tell the user to export `UNITY=/path/to/Editor/Unity` (install via Unity Hub with the "Windows Build Support (Mono)" module — see docs/07-WINDOWS-SETUP.md).

Report the tail of `Builds/log-Windows.txt` on failure.
