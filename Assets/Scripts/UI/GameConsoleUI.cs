using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Free Flight dev/cheat console — backtick/tilde (InputManager.ConsoleToggleAction)
    /// drops a command-line panel over the top of the HUD, same "game keeps running
    /// behind it" convention a Quake/Source-style console uses (does NOT set
    /// Time.timeScale = 0 the way the pause menu does). While open,
    /// InputManager.SetGameplayInputEnabled(false) disables every actual gameplay
    /// action so hotkey letters typed as part of a command (e.g. the 'g'/'u'/'b'/'r'/
    /// 'i'/'c' in "speed 500") don't also fire Parachute/Smoke/DroneFlip/Reset/
    /// PayloadDrop/Camera while the player is just typing.
    ///
    /// Commands are a simple name -> handler table (see RegisterCommands) so adding a
    /// new one is a single dictionary entry + method; every cheat implemented this way
    /// MUST also be documented in docs/10-CHEATS.md — see the add-cheat skill
    /// (.claude/commands/add-cheat.md), which exists specifically to keep that file
    /// current whenever a new command is added here.
    /// </summary>
    public class GameConsoleUI : MonoBehaviour
    {
        private const int MaxLogLines = 14;

        private Canvas _canvas;
        private DroneFlightController _flight;
        private RectTransform _panel;
        private TMPro.TextMeshProUGUI _logLabel;
        private TMPro.TMP_InputField _inputField;
        private bool _open;

        private readonly List<string> _lines = new();
        private Dictionary<string, Action<string[]>> _commands;

        public void Init(Canvas canvas, DroneFlightController flight)
        {
            _canvas = canvas;
            _flight = flight;
            RegisterCommands();
            Build();
            Log("AeroTerra dev console — type 'help' for a list of commands.");
            _panel.gameObject.SetActive(false);
        }

        private void RegisterCommands()
        {
            _commands = new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase)
            {
                ["help"] = HandleHelp,
                ["speed"] = HandleSpeed,
            };
        }

        private void Build()
        {
            _panel = Panel_(_canvas.transform, "GameConsole", new Color(0.03f, 0.04f, 0.06f, 0.90f),
                            new Vector2(0.02f, 0.55f), new Vector2(0.55f, 0.97f));
            Panel_(_panel, "BottomBorder", Accent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -2), new Vector2(0, 0));

            Label(_panel, "CONSOLE  —  ~ to close  —  'help' for commands", 14,
                  new Vector2(0.02f, 0.90f), new Vector2(0.98f, 0.98f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);

            _logLabel = Label(_panel, "", 13, new Vector2(0.02f, 0.16f), new Vector2(0.98f, 0.88f),
                              TextDim, TMPro.TextAlignmentOptions.BottomLeft);
            _logLabel.enableWordWrapping = true;

            _inputField = Input_(_panel, "type a command…", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.13f));
            _inputField.lineType = TMPro.TMP_InputField.LineType.SingleLine;
            _inputField.onSubmit.AddListener(OnSubmit);
        }

        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im?.ConsoleToggleAction != null && im.ConsoleToggleAction.WasPressedThisFrame())
            {
                SetOpen(!_open);
                return;
            }

            if (!_open) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _panel.gameObject.SetActive(open);
            AeroTerra.Input.InputManager.Instance?.SetGameplayInputEnabled(!open);

            if (open)
            {
                // Always start from an empty prompt — also sidesteps the toggle key
                // itself (backquote) possibly having been typed into the field the
                // instant it gained focus this same frame.
                _inputField.text = "";
                _inputField.ActivateInputField();
                _inputField.Select();
            }
            else
            {
                _inputField.DeactivateInputField();
            }
        }

        private void OnSubmit(string text)
        {
            if (!_open) return;
            ExecuteCommand(text);
            _inputField.text = "";
            _inputField.ActivateInputField();
        }

        private void ExecuteCommand(string raw)
        {
            string trimmed = (raw ?? "").Trim();
            if (trimmed.Length == 0) return;
            Log("> " + trimmed);

            var parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            if (_commands.TryGetValue(cmd, out var handler)) handler(args);
            else Log($"Unknown command: '{cmd}' — type 'help' for a list.");
        }

        private void Log(string line)
        {
            _lines.Add(line);
            while (_lines.Count > MaxLogLines) _lines.RemoveAt(0);
            _logLabel.text = string.Join("\n", _lines);
        }

        // ---------------------------------------------------------------
        // Commands — see docs/10-CHEATS.md for the player-facing reference.
        // ---------------------------------------------------------------

        private void HandleHelp(string[] args)
        {
            Log("Commands:");
            foreach (var name in _commands.Keys) Log("  " + name);
            Log("See docs/10-CHEATS.md for full usage.");
        }

        /// <summary>"speed &lt;km/h&gt;" — overrides the current drone's max speed cap
        /// (DroneFlightController.EffectiveMaxSpeedKmh), replacing Spec.MaxSpeedKmh
        /// everywhere the flight model references its own top speed (the hard velocity
        /// clamp, VTOL wing-lift transition, fixed-wing cruise/stall thresholds) so the
        /// whole flight envelope scales coherently instead of just capping raw velocity
        /// against otherwise-unchanged internal thresholds. Floor-clamped to 0 km/h —
        /// SetMaxSpeedOverride itself won't accept a negative value.</summary>
        private void HandleSpeed(string[] args)
        {
            if (_flight == null) { Log("No active drone."); return; }
            if (args.Length < 1 || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float kmh))
            {
                Log("Usage: speed <km/h>   e.g. speed 500");
                return;
            }
            _flight.SetMaxSpeedOverride(kmh);
            Log($"Max speed set to {_flight.MaxSpeedOverrideKmh:0} km/h.");
        }
    }
}
