using Rysy.Graphics;
using Rysy.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace Rysy.Graphics.Models;

/// <summary>
/// Represents a 3D model that can be rendered with sprite overlays
/// </summary>
public class Model3D : IDisposable {
    public string Name { get; set; } = "";
    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public BasicEffect? Effect { get; private set; }
    public Texture2D? Texture { get; private set; }
    
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero;
    public Vector3 Scale { get; set; } = Vector3.One;
    
    public List<SpriteOverlay> SpriteOverlays { get; } = new();
    
    private VertexPositionNormalTexture[]? _vertices;
    private short[]? _indices;
    private GraphicsDevice? _graphicsDevice;
    private bool _disposed = false;

    public Model3D(string name, GraphicsDevice graphicsDevice) {
        Name = name;
        _graphicsDevice = graphicsDevice;
        Effect = new BasicEffect(graphicsDevice);
        InitializeEffect();
    }

    private void InitializeEffect() {
        if (Effect == null) return;
        
        Effect.EnableDefaultLighting();
        Effect.TextureEnabled = true;
        Effect.VertexColorEnabled = false;
        Effect.PreferPerPixelLighting = true;
    }

    /// <summary>
    /// Loads model data from a simple OBJ-like format
    /// </summary>
    public void LoadFromData(VertexPositionNormalTexture[] vertices, short[] indices) {
        if (_graphicsDevice == null) return;

        _vertices = vertices;
        _indices = indices;

        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();

        VertexBuffer = new VertexBuffer(_graphicsDevice, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
        VertexBuffer.SetData(vertices);

        IndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        IndexBuffer.SetData(indices);
    }

    /// <summary>
    /// Sets the texture for this model
    /// </summary>
    public void SetTexture(Texture2D texture) {
        Texture = texture;
        if (Effect != null) {
            Effect.Texture = texture;
        }
    }

    /// <summary>
    /// Adds a sprite overlay at a specific 3D position on the model
    /// </summary>
    public void AddSpriteOverlay(string spriteName, Vector3 attachPoint, Vector2 size, float depth = 0f) {
        var overlay = new SpriteOverlay {
            SpriteName = spriteName,
            AttachPoint = attachPoint,
            Size = size,
            Depth = depth
        };
        SpriteOverlays.Add(overlay);
    }

    /// <summary>
    /// Renders the 3D model
    /// </summary>
    public void Render(Matrix world, Matrix view, Matrix projection) {
        if (_graphicsDevice == null || Effect == null || VertexBuffer == null || IndexBuffer == null) return;

        // Set up matrices
        var worldMatrix = Matrix.CreateScale(Scale) * 
                         Matrix.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z) *
                         Matrix.CreateTranslation(Position);

        Effect.World = worldMatrix * world;
        Effect.View = view;
        Effect.Projection = projection;

        // Set up graphics device
        _graphicsDevice.SetVertexBuffer(VertexBuffer);
        _graphicsDevice.Indices = IndexBuffer;

        // Render each pass
        foreach (var pass in Effect.CurrentTechnique.Passes) {
            pass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 0, _indices?.Length / 3 ?? 0, 0);
        }
    }

    /// <summary>
    /// Renders sprite overlays projected from 3D space to 2D screen space
    /// </summary>
    public void RenderSpriteOverlays(Matrix world, Matrix view, Matrix projection, SpriteBatch spriteBatch) {
        if (_graphicsDevice == null) return;

        var worldMatrix = Matrix.CreateScale(Scale) * 
                         Matrix.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z) *
                         Matrix.CreateTranslation(Position);

        var fullTransform = worldMatrix * world * view * projection;

        foreach (var overlay in SpriteOverlays) {
            var worldPos = Vector3.Transform(overlay.AttachPoint, fullTransform);
            
            // Convert to screen coordinates
            var viewport = _graphicsDevice.Viewport;
            var screenPos = new Vector2(
                (worldPos.X + 1) * 0.5f * viewport.Width,
                (1 - worldPos.Y) * 0.5f * viewport.Height
            );

            // Only render if in front of camera (positive Z)
            if (worldPos.Z > 0 && worldPos.Z < 1) {
                overlay.Render(spriteBatch, screenPos);
            }
        }
    }

    public void Dispose() {
        if (_disposed) return;

        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        Effect?.Dispose();
        
        _disposed = true;
    }
}

/// <summary>
/// Represents a sprite overlay attached to a 3D model
/// </summary>
public class SpriteOverlay {
    public string SpriteName { get; set; } = "";
    public Vector3 AttachPoint { get; set; } = Vector3.Zero;
    public Vector2 Size { get; set; } = new(32, 32);
    public float Depth { get; set; } = 0f;
    public Color Tint { get; set; } = Color.White;
    public float Alpha { get; set; } = 1f;
    public Vector2 Origin { get; set; } = new(0.5f, 0.5f);

    public void Render(SpriteBatch spriteBatch, Vector2 screenPosition) {
        var texture = GFX.Atlas[SpriteName];
        if (texture?.Texture == null) return;

        var drawPos = screenPosition - Size * Origin;
        var sourceRect = new Rectangle(texture.ClipRect.X, texture.ClipRect.Y, texture.Width, texture.Height);
        var destRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)Size.X, (int)Size.Y);

        spriteBatch.Draw(texture.Texture, destRect, sourceRect, Tint * Alpha, 0f, Vector2.Zero, SpriteEffects.None, Depth);
    }
}