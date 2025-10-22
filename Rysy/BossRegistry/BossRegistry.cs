using Rysy.Helpers;
using System.Reflection;

namespace Rysy.BossRegistry;

/// <summary>
/// Represents different types of boss entities
/// </summary>
public enum BossType {
    Boss,
    MidBoss,
    MiniBoss,
    Enemy,
    ExBoss
}

/// <summary>
/// Information about a boss entity
/// </summary>
public class BossInfo {
    public string EntityName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public BossType Type { get; set; }
    public string Description { get; set; } = "";
    public int Difficulty { get; set; } = 1; // 1-10 scale
    public string? SpriteTexture { get; set; }
    public Vector2 DefaultSize { get; set; } = new(32, 32);
    public Dictionary<string, object> DefaultAttributes { get; set; } = new();
    public string[] RequiredMods { get; set; } = Array.Empty<string>();
    public string? SourceMod { get; set; }

    public BossInfo(string entityName, string displayName, BossType type) {
        EntityName = entityName;
        DisplayName = displayName;
        Type = type;
    }
}

/// <summary>
/// Registry for managing boss entities in the map editor
/// </summary>
public static class BossRegistry {
    private static readonly Dictionary<string, BossInfo> _bosses = new();
    private static readonly Dictionary<BossType, List<BossInfo>> _bossesByType = new();
    private static bool _initialized = false;

    public static IReadOnlyDictionary<string, BossInfo> AllBosses => _bosses;
    public static IReadOnlyDictionary<BossType, List<BossInfo>> BossesByType => _bossesByType;

    public static void Initialize() {
        if (_initialized) return;

        Logger.Write("BossRegistry", LogLevel.Info, "Initializing boss registry");
        
        // Initialize boss type lists
        foreach (BossType type in Enum.GetValues<BossType>()) {
            _bossesByType[type] = new List<BossInfo>();
        }

        // Register vanilla bosses
        RegisterVanillaBosses();
        
        _initialized = true;
        Logger.Write("BossRegistry", LogLevel.Info, $"Registered {_bosses.Count} bosses");
    }

    private static void RegisterVanillaBosses() {
        // Main Bosses
        RegisterBoss(new BossInfo("badelineBoss", "Badeline Boss", BossType.Boss) {
            Description = "The final boss encounter with Badeline in Chapter 6",
            Difficulty = 8,
            SpriteTexture = "characters/badeline/boss/boss00",
            DefaultSize = new(24, 24),
            DefaultAttributes = new() {
                ["shootingPattern"] = 0,
                ["cameraOffsetX"] = 0,
                ["cameraOffsetY"] = 0,
                ["playerStartX"] = 0,
                ["playerStartY"] = 0
            }
        });

        RegisterBoss(new BossInfo("oshiroBoss", "Oshiro Boss", BossType.Boss) {
            Description = "The ghostly hotel owner boss from Chapter 3",
            Difficulty = 6,
            SpriteTexture = "characters/oshiro/boss/oshiro00",
            DefaultSize = new(32, 32),
            DefaultAttributes = new() {
                ["followPlayer"] = true
            }
        });

        // Mid Bosses
        RegisterBoss(new BossInfo("crushBlock", "Kevin (Crush Block)", BossType.MidBoss) {
            Description = "Large moving crushing block",
            Difficulty = 5,
            SpriteTexture = "objects/crushblock/block00",
            DefaultSize = new(32, 32),
            DefaultAttributes = new() {
                ["axes"] = "both",
                ["chillout"] = false
            }
        });

        // Mini Bosses  
        RegisterBoss(new BossInfo("seeker", "Seeker", BossType.MiniBoss) {
            Description = "Flying enemy that chases the player",
            Difficulty = 4,
            SpriteTexture = "characters/seeker/seeker00",
            DefaultSize = new(12, 12),
            DefaultAttributes = new() { }
        });

        RegisterBoss(new BossInfo("badelineChaser", "Badeline Chaser", BossType.MiniBoss) {
            Description = "Badeline that chases the player",
            Difficulty = 5,
            SpriteTexture = "characters/badeline/sleep00",
            DefaultSize = new(8, 11),
            DefaultAttributes = new() {
                ["canChangeMusic"] = true
            }
        });

        // Enemies
        RegisterBoss(new BossInfo("puffer", "Puffer Fish", BossType.Enemy) {
            Description = "Explosive puffer fish enemy",
            Difficulty = 2,
            SpriteTexture = "objects/puffer/idle00",
            DefaultSize = new(16, 16),
            DefaultAttributes = new() {
                ["right"] = false
            }
        });

        RegisterBoss(new BossInfo("spinner", "Crystal Spinner", BossType.Enemy) {
            Description = "Rotating crystal hazard",
            Difficulty = 2,
            SpriteTexture = "danger/crystal/fg_white00",
            DefaultSize = new(16, 16),
            DefaultAttributes = new() {
                ["color"] = "Blue",
                ["attachToSolid"] = true
            }
        });

        RegisterBoss(new BossInfo("trackSpinner", "Track Spinner", BossType.Enemy) {
            Description = "Spinner that moves along a track",
            Difficulty = 3,
            SpriteTexture = "danger/crystal/fg_white00",
            DefaultSize = new(16, 16),
            DefaultAttributes = new() {
                ["color"] = "Blue",
                ["speed"] = "Normal"
            }
        });

        RegisterBoss(new BossInfo("rotateSpinner", "Rotate Spinner", BossType.Enemy) {
            Description = "Spinner that rotates around a center point",
            Difficulty = 3,
            SpriteTexture = "danger/crystal/fg_white00",
            DefaultSize = new(16, 16),
            DefaultAttributes = new() {
                ["color"] = "Blue",
                ["clockwise"] = true,
                ["length"] = 32
            }
        });

        // Ex Bosses (modded/challenge variants)
        RegisterBoss(new BossInfo("badelineBoss_ex", "Badeline Boss EX", BossType.ExBoss) {
            Description = "Enhanced version of the Badeline boss with increased difficulty",
            Difficulty = 10,
            SpriteTexture = "characters/badeline/boss/boss00",
            DefaultSize = new(24, 24),
            DefaultAttributes = new() {
                ["shootingPattern"] = 15, // Hardest pattern
                ["cameraOffsetX"] = 0,
                ["cameraOffsetY"] = 0,
                ["playerStartX"] = 0,
                ["playerStartY"] = 0,
                ["enhanced"] = true
            }
        });
    }

    /// <summary>
    /// Registers a new boss in the registry
    /// </summary>
    public static void RegisterBoss(BossInfo boss) {
        _bosses[boss.EntityName] = boss;
        _bossesByType[boss.Type].Add(boss);
        
        Logger.Write("BossRegistry", LogLevel.Debug, $"Registered {boss.Type} boss: {boss.DisplayName}");
    }

    /// <summary>
    /// Gets boss information by entity name
    /// </summary>
    public static BossInfo? GetBoss(string entityName) {
        return _bosses.TryGetValue(entityName, out var boss) ? boss : null;
    }

    /// <summary>
    /// Gets all bosses of a specific type
    /// </summary>
    public static IReadOnlyList<BossInfo> GetBossesByType(BossType type) {
        return _bossesByType.TryGetValue(type, out var bosses) ? bosses : Array.Empty<BossInfo>();
    }

    /// <summary>
    /// Checks if an entity is registered as a boss
    /// </summary>
    public static bool IsBoss(string entityName) {
        return _bosses.ContainsKey(entityName);
    }

    /// <summary>
    /// Gets boss type for an entity, or null if not a boss
    /// </summary>
    public static BossType? GetBossType(string entityName) {
        return GetBoss(entityName)?.Type;
    }

    /// <summary>
    /// Registers bosses from mod assemblies
    /// </summary>
    public static void RegisterModBosses(string modName, Assembly assembly) {
        try {
            var bossTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<BossEntityAttribute>() != null);

            foreach (var type in bossTypes) {
                var attr = type.GetCustomAttribute<BossEntityAttribute>()!;
                var boss = new BossInfo(attr.EntityName, attr.DisplayName, attr.BossType) {
                    Description = attr.Description,
                    Difficulty = attr.Difficulty,
                    SourceMod = modName
                };

                RegisterBoss(boss);
            }
        } catch (Exception ex) {
            Logger.Write("BossRegistry", LogLevel.Warning, $"Failed to register bosses from mod {modName}: {ex.Message}");
        }
    }
}

/// <summary>
/// Attribute for marking boss entities in mods
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class BossEntityAttribute : Attribute {
    public string EntityName { get; }
    public string DisplayName { get; }
    public BossType BossType { get; }
    public string Description { get; set; } = "";
    public int Difficulty { get; set; } = 1;

    public BossEntityAttribute(string entityName, string displayName, BossType bossType) {
        EntityName = entityName;
        DisplayName = displayName;
        BossType = bossType;
    }
}