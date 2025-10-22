using Rysy.Graphics.Models;
using Rysy.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Rysy.Graphics;
using Rysy.Selections;
using Rysy.Mods;

namespace Rysy.Entities.Model3D;

/// <summary>
/// A 3D model entity that can be placed in the map editor
/// </summary>
[CustomEntity("model3d")]
public class Model3DEntity : Entity, IPlaceable {
    private Graphics.Models.Model3D? _model;
    private string _modelName = "";
    private bool _modelLoaded = false;

    public override int Depth => -100; // Render before most 2D elements

    public static FieldList GetFields() => new(new {
        modelName = Fields.String("").WithName("Model Name"),
        modelFile = Fields.Path("", "Models", "obj,ply,model3d").WithName("Model File"),
        scaleX = 1.0f,
        scaleY = 1.0f,
        scaleZ = 1.0f,
        rotationX = 0.0f,
        rotationY = 0.0f,
        rotationZ = 0.0f,
        textureFile = Fields.Path("", "Graphics/Atlases/Gameplay", "png").WithName("Texture"),
        enableLighting = true,
        castShadows = false,
        sprites = Fields.String("").WithName("Sprite Overlays (comma-separated)")
    });

    public static PlacementList GetPlacements() => new("3D Model");

    public override void OnChanged(EntityDataChangeCtx changed) {
        base.OnChanged(changed);
        
        // Reload model if relevant fields changed
        if (changed.AllChanged || 
            changed.IsChanged("modelFile") || 
            changed.IsChanged("modelName") ||
            changed.IsChanged("scaleX") ||
            changed.IsChanged("scaleY") ||
            changed.IsChanged("scaleZ") ||
            changed.IsChanged("rotationX") ||
            changed.IsChanged("rotationY") ||
            changed.IsChanged("rotationZ") ||
            changed.IsChanged("textureFile") ||
            changed.IsChanged("sprites")) {
            LoadModel();
        }
    }

    private void LoadModel() {
        var modelFile = Attr("modelFile", "");
        _modelName = Attr("modelName", "");

        if (string.IsNullOrEmpty(modelFile) || string.IsNullOrEmpty(_modelName)) {
            Logger.Write("Model3DEntity", LogLevel.Warning, "Model file or name not specified");
            return;
        }

        try {
            // Try to load from mod assets first
            var modAssetPath = Path.Combine("Models", modelFile);
            var fullPath = Room?.Map?.Mod?.Filesystem?.OpenFile(modAssetPath, stream => 
                Room.Map.Mod.Filesystem.Root + "/" + modAssetPath);
            
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) {
                // Try direct path if the file path is absolute
                if (Path.IsPathRooted(modelFile) && File.Exists(modelFile)) {
                    fullPath = modelFile;
                } else {
                    Logger.Write("Model3DEntity", LogLevel.Warning, $"Model file not found: {modelFile}");
                    return;
                }
            }

            _model = Model3DManager.LoadModel(_modelName, fullPath);
            if (_model != null) {
                SetupModel();
                _modelLoaded = true;
                Logger.Write("Model3DEntity", LogLevel.Info, $"Loaded 3D model: {_modelName}");
            }
        } catch (Exception ex) {
            Logger.Write("Model3DEntity", LogLevel.Error, $"Failed to load 3D model: {ex.Message}");
        }
    }

    private void SetupModel() {
        if (_model == null) return;

        // Apply scale
        var scaleX = Float("scaleX", 1.0f);
        var scaleY = Float("scaleY", 1.0f);
        var scaleZ = Float("scaleZ", 1.0f);
        _model.Scale = new Vector3(scaleX, scaleY, scaleZ);

        // Apply rotation (convert from degrees to radians)
        var rotX = MathHelper.ToRadians(Float("rotationX", 0.0f));
        var rotY = MathHelper.ToRadians(Float("rotationY", 0.0f));
        var rotZ = MathHelper.ToRadians(Float("rotationZ", 0.0f));
        _model.Rotation = new Vector3(rotX, rotY, rotZ);

        // Load texture if specified
        var textureFile = Attr("textureFile", "");
        if (!string.IsNullOrEmpty(textureFile)) {
            var texture = GFX.Atlas[textureFile];
            if (texture?.Texture != null) {
                _model.SetTexture(texture.Texture);
            }
        }

        // Setup sprite overlays
        SetupSpriteOverlays();
    }

    private void SetupSpriteOverlays() {
        if (_model == null) return;

        var spritesString = Attr("sprites", "");
        if (string.IsNullOrEmpty(spritesString)) return;

        var sprites = spritesString.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var sprite in sprites) {
            var parts = sprite.Trim().Split(':');
            if (parts.Length >= 1) {
                var spriteName = parts[0].Trim();
                var attachPoint = Vector3.Zero;
                var size = new Vector2(32, 32);

                // Parse attachment point if provided
                if (parts.Length >= 2 && parts[1].Contains('|')) {
                    var coords = parts[1].Split('|');
                    if (coords.Length >= 3) {
                        if (float.TryParse(coords[0], out var x) &&
                            float.TryParse(coords[1], out var y) &&
                            float.TryParse(coords[2], out var z)) {
                            attachPoint = new Vector3(x, y, z);
                        }
                    }
                }

                // Parse size if provided
                if (parts.Length >= 3 && parts[2].Contains('x')) {
                    var sizeParts = parts[2].Split('x');
                    if (sizeParts.Length >= 2) {
                        if (float.TryParse(sizeParts[0], out var w) &&
                            float.TryParse(sizeParts[1], out var h)) {
                            size = new Vector2(w, h);
                        }
                    }
                }

                _model.AddSpriteOverlay(spriteName, attachPoint, size);
                Logger.Write("Model3DEntity", LogLevel.Debug, $"Added sprite overlay: {spriteName}");
            }
        }
    }

    public override IEnumerable<ISprite> GetSprites() {
        if (!_modelLoaded || _model == null) {
            // Render a placeholder sprite
            return GetPlaceholderSprites();
        }

        // Update model position
        _model.Position = new Vector3(Pos.X, Pos.Y, 0);

        // Since we can't directly render 3D models in the 2D sprite system,
        // we'll render a placeholder that represents the 3D model
        var placeholderSprites = new List<ISprite>();
        
        // Add a wireframe outline to represent the 3D model bounds
        placeholderSprites.Add(ISprite.OutlinedRect(Pos - new Vector2(16, 16), 32, 32, Color.Blue * 0.3f, Color.Blue));
        
        // Add a center indicator
        placeholderSprites.Add(ISprite.OutlinedRect(Pos - new Vector2(2, 2), 4, 4, Color.Cyan * 0.8f, Color.Cyan));

        // Render model name as text if available
        if (!string.IsNullOrEmpty(_modelName)) {
            // Note: Text rendering would need to be implemented differently in the actual sprite system
            // This is a placeholder for the concept
        }

        return placeholderSprites;
    }

    private IEnumerable<ISprite> GetPlaceholderSprites() {
        // Render a simple sprite as placeholder when model isn't loaded
        var placeholderSprite = ISprite.FromTexture(Pos, "Rysy:__fallback");
        yield return placeholderSprite;

        // Add error indicator
        yield return ISprite.OutlinedRect(Pos - new Vector2(16, 16), 32, 32, Color.Red * 0.3f, Color.Red);
    }

    public override ISelectionCollider GetMainSelection() {
        return ISelectionCollider.FromRect(new Rectangle((int)Pos.X - 16, (int)Pos.Y - 16, 32, 32));
    }
}