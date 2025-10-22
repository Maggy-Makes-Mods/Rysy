using ImGuiNET;
using Rysy.BossRegistry;
using Rysy.Gui.Windows;
using Rysy.Graphics;
using Rysy.Helpers;
using System.Numerics;

namespace Rysy.Gui.Windows;

/// <summary>
/// Window for browsing and placing boss entities
/// </summary>
public class BossListWindow : Window {
    private BossType _selectedType = BossType.Boss;
    private BossInfo? _selectedBoss = null;
    private string _searchFilter = "";
    private bool _showOnlyAvailable = true;

    public BossListWindow() : base("Boss List", new(600, 500)) {
        NoSaveData = true;
    }

    protected override void Render() {
        base.Render();

        // Boss type filter tabs
        if (ImGui.BeginTabBar("BossTypes")) {
            foreach (BossType type in Enum.GetValues<BossType>()) {
                if (ImGui.BeginTabItem(type.ToString())) {
                    _selectedType = type;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }

        ImGui.Separator();

        // Search and filters
        ImGui.InputText("Search", ref _searchFilter, 256);
        ImGui.SameLine();
        ImGui.Checkbox("Show only available", ref _showOnlyAvailable);

        ImGui.Separator();

        // Boss list
        var bosses = BossRegistry.BossRegistry.GetBossesByType(_selectedType)
            .Where(b => string.IsNullOrEmpty(_searchFilter) ||
                       b.DisplayName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                       b.EntityName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

        if (_showOnlyAvailable) {
            bosses = bosses.Where(b => string.IsNullOrEmpty(b.SourceMod) ||
                                      Rysy.Mods.ModRegistry.GetModByName(b.SourceMod) != null);
        }

        // Two-column layout
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var leftWidth = availableWidth * 0.4f;

        if (ImGui.BeginChild("BossList", new(leftWidth, 0), ImGuiChildFlags.Borders)) {
            foreach (var boss in bosses) {
                var isSelected = _selectedBoss == boss;

                if (ImGui.Selectable($"{boss.DisplayName}###{boss.EntityName}", isSelected)) {
                    _selectedBoss = boss;
                }

                // Show difficulty as colored indicator
                ImGui.SameLine();
                var color = GetDifficultyColor(boss.Difficulty);
                ImGui.TextColored(color, $"[{boss.Difficulty}]");

                if (!string.IsNullOrEmpty(boss.SourceMod)) {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f), $"({boss.SourceMod})");
                }
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // Boss details panel
        if (ImGui.BeginChild("BossDetails", new(0, 0), ImGuiChildFlags.Borders)) {
            if (_selectedBoss != null) {
                RenderBossDetails(_selectedBoss);
            } else {
                ImGui.TextWrapped("Select a boss from the list to view details.");
            }
        }
        ImGui.EndChild();
    }

    private void RenderBossDetails(BossInfo boss) {
        ImGui.Text($"Name: {boss.DisplayName}");
        ImGui.Text($"Entity: {boss.EntityName}");
        ImGui.Text($"Type: {boss.Type}");

        var diffColor = GetDifficultyColor(boss.Difficulty);
        ImGui.Text("Difficulty: ");
        ImGui.SameLine();
        ImGui.TextColored(diffColor, $"{boss.Difficulty}/10");

        if (!string.IsNullOrEmpty(boss.SourceMod)) {
            ImGui.Text($"Mod: {boss.SourceMod}");
        }

        ImGui.Separator();

        if (!string.IsNullOrEmpty(boss.Description)) {
            ImGui.TextWrapped(boss.Description);
            ImGui.Separator();
        }

        // Sprite preview
        if (!string.IsNullOrEmpty(boss.SpriteTexture)) {
            var texture = GFX.Atlas[boss.SpriteTexture];
            if (texture != null) {
                ImGui.Text("Preview:");
                ImGui.Text($"Sprite: {boss.SpriteTexture}");
                // TODO: Fix texture rendering
                // var scale = Math.Min(64f / texture.Width, 64f / texture.Height);
                // var tex2d = texture.Texture;
                // if (tex2d != null) {
                //     ImGui.Image((nint) tex2d.Handle,
                //                new Vector2(texture.Width * scale, texture.Height * scale));
                // }
            }
        }

        ImGui.Separator();

        // Default attributes
        if (boss.DefaultAttributes.Count > 0) {
            ImGui.Text("Default Attributes:");
            foreach (var attr in boss.DefaultAttributes) {
                ImGui.Text($"  {attr.Key}: {attr.Value}");
            }
            ImGui.Separator();
        }

        // Action buttons
        if (ImGui.Button("Place in Current Room")) {
            PlaceBossInCurrentRoom(boss);
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy Entity Name")) {
            ImGui.SetClipboardText(boss.EntityName);
        }

        if (!string.IsNullOrEmpty(boss.SourceMod)) {
            ImGui.SameLine();
            var modLoaded = Rysy.Mods.ModRegistry.GetModByName(boss.SourceMod) != null;
            if (!modLoaded) {
                ImGui.BeginDisabled();
                ImGui.Button("Mod Not Loaded");
                ImGui.EndDisabled();
            }
        }
    }

    private System.Numerics.Vector4 GetDifficultyColor(int difficulty) {
        return difficulty switch {
            <= 2 => new System.Numerics.Vector4(0.0f, 1.0f, 0.0f, 1.0f), // Green - Easy
            <= 4 => new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f), // Yellow - Medium
            <= 6 => new System.Numerics.Vector4(1.0f, 0.5f, 0.0f, 1.0f), // Orange - Hard
            <= 8 => new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f), // Red - Very Hard
            _ => new System.Numerics.Vector4(0.5f, 0.0f, 1.0f, 1.0f),     // Purple - Extreme
        };
    }

    private void PlaceBossInCurrentRoom(BossInfo boss) {
        if (EditorState.CurrentRoom == null) {
            Logger.Write("BossListWindow", LogLevel.Warning, "No current room to place boss in");
            return;
        }

        try {
            var entity = EntityRegistry.Create(new(boss.EntityName) {
                Attributes = new Dictionary<string, object>(boss.DefaultAttributes)
            }, EditorState.CurrentRoom, false);

            if (entity != null) {
                // Place at camera center or mouse position
                var camera = EditorState.Camera;
                if (camera != null) {
                    var centerPos = camera.Pos;
                    entity.Pos = centerPos - EditorState.CurrentRoom.Pos;
                }

                EditorState.CurrentRoom.Entities.Add(entity);
                EditorState.CurrentRoom.ClearRenderCache();

                // Add to history
                if (EditorState.History != null) {
                    EditorState.History.ApplyNewAction(new History.AddEntityAction(entity, EditorState.CurrentRoom));
                }

                Logger.Write("BossListWindow", LogLevel.Info, $"Placed {boss.DisplayName} in room {EditorState.CurrentRoom.Name}");
            }
        } catch (Exception ex) {
            Logger.Write("BossListWindow", LogLevel.Error, $"Failed to place boss {boss.EntityName}: {ex.Message}");
        }
    }
}