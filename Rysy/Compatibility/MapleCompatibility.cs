using Rysy.Helpers;
using Rysy.Mods;
using System.Text.Json;

namespace Rysy.Compatibility;

/// <summary>
/// Provides backward compatibility support for Maple editor legacy assets and functionality
/// </summary>
public static class MapleCompatibility {
    private static bool _initialized = false;
    private static readonly Dictionary<string, string> _mapleEntityMappings = new();
    private static readonly Dictionary<string, string> _mapleStyleMappings = new();

    public static void Initialize() {
        if (_initialized) return;
        
        Logger.Write("MapleCompatibility", LogLevel.Info, "Initializing Maple compatibility layer");
        
        // Legacy Maple entity mappings
        _mapleEntityMappings.Clear();
        foreach (var kvp in new Dictionary<string, string> {
            ["maple_player_spawn"] = "player",
            ["maple_berry"] = "strawberry",
            ["maple_dash_crystal"] = "refill",
            ["maple_bouncer"] = "spring",
            ["maple_save_point"] = "checkpoint",
            ["maple_death_spikes"] = "spikes",
            ["maple_death_zone"] = "killbox",
            ["maple_platform"] = "jumpThru",
            ["maple_wall"] = "solid",
            ["maple_danger_spinner"] = "spinner",
            ["maple_locked_door"] = "door",
            ["maple_golden_key"] = "key",
            ["maple_button"] = "touchSwitch",
            ["maple_gate"] = "switchGate",
            ["maple_breaking_blocks"] = "crumbleBlock",
            ["maple_falling_blocks"] = "fallingBlock",
            ["maple_moving_block"] = "movingPlatform",
            ["maple_dash_booster"] = "booster",
            ["maple_dash_block"] = "dashBlock",
            ["maple_dream_block"] = "dreamBlock",
            ["maple_cassette_block"] = "cassetteBlock",
            ["maple_zip_line"] = "zipMover",
            ["maple_crusher"] = "crushBlock",
            ["maple_chaser"] = "seeker",
            ["maple_ghost_boss"] = "oshiroBoss",
            ["maple_mirror_boss"] = "badelineBoss"
        }) {
            _mapleEntityMappings.Add(kvp.Key, kvp.Value);
        }

        // Legacy Maple styleground mappings
        _mapleStyleMappings.Clear();
        foreach (var kvp in new Dictionary<string, string> {
            ["maple_bg_parallax"] = "parallax",
            ["maple_fg_overlay"] = "parallax",
            ["maple_animated_bg"] = "style",
            ["maple_gradient"] = "style",
            ["maple_particle_system"] = "style"
        }) {
            _mapleStyleMappings.Add(kvp.Key, kvp.Value);
        }

        _initialized = true;
        Logger.Write("MapleCompatibility", LogLevel.Info, $"Loaded {_mapleEntityMappings.Count} entity mappings and {_mapleStyleMappings.Count} style mappings");
    }

    /// <summary>
    /// Converts legacy Maple entity name to modern Rysy/Celeste entity name
    /// </summary>
    public static string ConvertEntityName(string mapleName) {
        if (_mapleEntityMappings.TryGetValue(mapleName, out var modernName)) {
            Logger.Write("MapleCompatibility", LogLevel.Debug, $"Converted entity '{mapleName}' to '{modernName}'");
            return modernName;
        }
        return mapleName;
    }

    /// <summary>
    /// Converts legacy Maple styleground name to modern Rysy/Celeste styleground name
    /// </summary>
    public static string ConvertStyleName(string mapleName) {
        if (_mapleStyleMappings.TryGetValue(mapleName, out var modernName)) {
            Logger.Write("MapleCompatibility", LogLevel.Debug, $"Converted style '{mapleName}' to '{modernName}'");
            return modernName;
        }
        return mapleName;
    }

    /// <summary>
    /// Loads legacy Maple configuration if present
    /// </summary>
    public static async Task LoadLegacyConfigAsync(string modDirectory) {
        var mapleConfigPath = Path.Combine(modDirectory, "maple_config.json");
        if (!File.Exists(mapleConfigPath)) return;

        try {
            var configContent = await File.ReadAllTextAsync(mapleConfigPath);
            var config = JsonSerializer.Deserialize<JsonElement>(configContent);
            
            Logger.Write("MapleCompatibility", LogLevel.Info, $"Found legacy Maple config at: {mapleConfigPath}");
            
            // Process legacy config and integrate with modern settings
            
        } catch (Exception ex) {
            Logger.Write("MapleCompatibility", LogLevel.Warning, $"Failed to load Maple config: {ex.Message}");
        }
    }
}