using Rysy.Helpers;
using Rysy.Mods;
using System.Text.Json;

namespace Rysy.Compatibility;

/// <summary>
/// Provides backward compatibility support for Ahorn editor legacy assets and functionality
/// </summary>
public static class AhornCompatibility {
    private static bool _initialized = false;
    private static readonly Dictionary<string, string> _ahornEntityMappings = new();
    private static readonly Dictionary<string, string> _ahornTriggerMappings = new();

    public static void Initialize() {
        if (_initialized) return;
        
        Logger.Write("AhornCompatibility", LogLevel.Info, "Initializing Ahorn compatibility layer");
        
        // Legacy Ahorn entity mappings
        _ahornEntityMappings.Clear();
        foreach (var kvp in new Dictionary<string, string> {
            ["ahorn_player"] = "player",
            ["ahorn_strawberry"] = "strawberry", 
            ["ahorn_refill"] = "refill",
            ["ahorn_spring"] = "spring",
            ["ahorn_checkpoint"] = "checkpoint",
            ["ahorn_spikes"] = "spikes",
            ["ahorn_killbox"] = "killbox",
            ["ahorn_jumpthru"] = "jumpThru",
            ["ahorn_solid"] = "solid",
            ["ahorn_spinner"] = "spinner",
            ["ahorn_door"] = "door",
            ["ahorn_key"] = "key",
            ["ahorn_switch"] = "touchSwitch",
            ["ahorn_gate"] = "switchGate",
            ["ahorn_crumble"] = "crumbleBlock",
            ["ahorn_fallingBlock"] = "fallingBlock",
            ["ahorn_movingPlatform"] = "movingPlatform",
            ["ahorn_booster"] = "booster",
            ["ahorn_dash_block"] = "dashBlock",
            ["ahorn_dream_block"] = "dreamBlock",
            ["ahorn_cassette_block"] = "cassetteBlock",
            ["ahorn_zip_mover"] = "zipMover",
            ["ahorn_kevin"] = "crushBlock",
            ["ahorn_seeker"] = "seeker",
            ["ahorn_oshiro"] = "oshiroBoss",
            ["ahorn_badeline"] = "badelineBoss"
        }) {
            _ahornEntityMappings.Add(kvp.Key, kvp.Value);
        }

        // Legacy Ahorn trigger mappings
        _ahornTriggerMappings.Clear();
        foreach (var kvp in new Dictionary<string, string> {
            ["ahorn_music_trigger"] = "musicTrigger",
            ["ahorn_camera_trigger"] = "cameraTargetTrigger",
            ["ahorn_wind_trigger"] = "windTrigger",
            ["ahorn_checkpoint_blocker"] = "checkpointBlockerTrigger",
            ["ahorn_event_trigger"] = "eventTrigger",
            ["ahorn_flag_trigger"] = "flagTrigger",
            ["ahorn_respawn_trigger"] = "changeRespawnTrigger"
        }) {
            _ahornTriggerMappings.Add(kvp.Key, kvp.Value);
        }

        _initialized = true;
        Logger.Write("AhornCompatibility", LogLevel.Info, $"Loaded {_ahornEntityMappings.Count} entity mappings and {_ahornTriggerMappings.Count} trigger mappings");
    }

    /// <summary>
    /// Converts legacy Ahorn entity name to modern Rysy/Celeste entity name
    /// </summary>
    public static string ConvertEntityName(string ahornName) {
        if (_ahornEntityMappings.TryGetValue(ahornName, out var modernName)) {
            Logger.Write("AhornCompatibility", LogLevel.Debug, $"Converted entity '{ahornName}' to '{modernName}'");
            return modernName;
        }
        return ahornName;
    }

    /// <summary>
    /// Converts legacy Ahorn trigger name to modern Rysy/Celeste trigger name
    /// </summary>
    public static string ConvertTriggerName(string ahornName) {
        if (_ahornTriggerMappings.TryGetValue(ahornName, out var modernName)) {
            Logger.Write("AhornCompatibility", LogLevel.Debug, $"Converted trigger '{ahornName}' to '{modernName}'");
            return modernName;
        }
        return ahornName;
    }

    /// <summary>
    /// Loads legacy Ahorn configuration if present
    /// </summary>
    public static async Task LoadLegacyConfigAsync(string modDirectory) {
        var ahornConfigPath = Path.Combine(modDirectory, "ahorn_config.json");
        if (!File.Exists(ahornConfigPath)) return;

        try {
            var configContent = await File.ReadAllTextAsync(ahornConfigPath);
            var config = JsonSerializer.Deserialize<JsonElement>(configContent);
            
            Logger.Write("AhornCompatibility", LogLevel.Info, $"Found legacy Ahorn config at: {ahornConfigPath}");
            
            // Process legacy config and integrate with modern settings
            // This would involve converting old Ahorn settings to Rysy settings format
            
        } catch (Exception ex) {
            Logger.Write("AhornCompatibility", LogLevel.Warning, $"Failed to load Ahorn config: {ex.Message}");
        }
    }
}