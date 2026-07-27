using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AeroTerra.Core
{
    /// <summary>
    /// JSON persistence for settings and custom drones.
    /// Files live in Application.persistentDataPath (works on all 5 platforms).
    /// </summary>
    public static class SaveSystem
    {
        private static string SettingsPath => Path.Combine(Application.persistentDataPath, "settings.json");
        private static string CustomDronesPath => Path.Combine(Application.persistentDataPath, "custom_drones.json");
        private static string FlightLogPath => Path.Combine(Application.persistentDataPath, "flight_log.json");

        public static SettingsData LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsPath)) ?? new SettingsData();
            }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] settings load failed: {e.Message}"); }
            return new SettingsData();
        }

        public static void SaveSettings(SettingsData data)
        {
            try { File.WriteAllText(SettingsPath, JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogError($"[SaveSystem] settings save failed: {e.Message}"); }
        }

        [Serializable] private class CustomDroneList { public List<Workshop.CustomDroneData> Items = new List<Workshop.CustomDroneData>(); }

        public static List<Workshop.CustomDroneData> LoadCustomDrones()
        {
            try
            {
                if (File.Exists(CustomDronesPath))
                    return JsonUtility.FromJson<CustomDroneList>(File.ReadAllText(CustomDronesPath))?.Items
                           ?? new List<Workshop.CustomDroneData>();
            }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] custom drones load failed: {e.Message}"); }
            return new List<Workshop.CustomDroneData>();
        }

        public static void SaveCustomDrones(List<Workshop.CustomDroneData> drones)
        {
            try { File.WriteAllText(CustomDronesPath, JsonUtility.ToJson(new CustomDroneList { Items = drones }, true)); }
            catch (Exception e) { Debug.LogError($"[SaveSystem] custom drones save failed: {e.Message}"); }
        }

        [Serializable] private class FlightLogList { public List<Workshop.DroneFlightLog> Items = new List<Workshop.DroneFlightLog>(); }

        public static List<Workshop.DroneFlightLog> LoadFlightLogs()
        {
            try
            {
                if (File.Exists(FlightLogPath))
                    return JsonUtility.FromJson<FlightLogList>(File.ReadAllText(FlightLogPath))?.Items
                           ?? new List<Workshop.DroneFlightLog>();
            }
            catch (Exception e) { Debug.LogWarning($"[SaveSystem] flight log load failed: {e.Message}"); }
            return new List<Workshop.DroneFlightLog>();
        }

        public static void SaveFlightLogs(List<Workshop.DroneFlightLog> logs)
        {
            try { File.WriteAllText(FlightLogPath, JsonUtility.ToJson(new FlightLogList { Items = logs }, true)); }
            catch (Exception e) { Debug.LogError($"[SaveSystem] flight log save failed: {e.Message}"); }
        }
    }
}
