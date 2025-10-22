using Rysy.Graphics.Models;
using Rysy.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace Rysy.Graphics.Models;

/// <summary>
/// Manages loading and rendering of 3D models in the editor
/// </summary>
public static class Model3DManager {
    private static readonly Dictionary<string, Model3D> _loadedModels = new();
    private static GraphicsDevice? _graphicsDevice;
    private static bool _initialized = false;

    public static void Initialize(GraphicsDevice graphicsDevice) {
        _graphicsDevice = graphicsDevice;
        _initialized = true;
        Logger.Write("Model3DManager", LogLevel.Info, "Initialized 3D model manager");
    }

    /// <summary>
    /// Loads a 3D model from file
    /// </summary>
    public static Model3D? LoadModel(string name, string filePath) {
        if (!_initialized || _graphicsDevice == null) {
            Logger.Write("Model3DManager", LogLevel.Warning, "Model3DManager not initialized");
            return null;
        }

        if (_loadedModels.TryGetValue(name, out var existing)) {
            return existing;
        }

        try {
            var model = new Model3D(name, _graphicsDevice);
            
            // Load model data based on file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension) {
                case ".obj":
                    LoadObjModel(model, filePath);
                    break;
                case ".ply":
                    LoadPlyModel(model, filePath);
                    break;
                case ".model3d":
                    LoadCustomModel(model, filePath);
                    break;
                default:
                    Logger.Write("Model3DManager", LogLevel.Warning, $"Unsupported model format: {extension}");
                    model.Dispose();
                    return null;
            }

            _loadedModels[name] = model;
            Logger.Write("Model3DManager", LogLevel.Info, $"Loaded 3D model: {name}");
            return model;

        } catch (Exception ex) {
            Logger.Write("Model3DManager", LogLevel.Error, $"Failed to load model {name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a simple primitive model
    /// </summary>
    public static Model3D? CreatePrimitiveModel(string name, PrimitiveType type, float size = 1f) {
        if (!_initialized || _graphicsDevice == null) return null;

        if (_loadedModels.TryGetValue(name, out var existing)) {
            return existing;
        }

        var model = new Model3D(name, _graphicsDevice);

        switch (type) {
            case PrimitiveType.Cube:
                CreateCube(model, size);
                break;
            case PrimitiveType.Sphere:
                CreateSphere(model, size, 16);
                break;
            case PrimitiveType.Plane:
                CreatePlane(model, size);
                break;
            default:
                model.Dispose();
                return null;
        }

        _loadedModels[name] = model;
        return model;
    }

    private static void CreateCube(Model3D model, float size) {
        var halfSize = size * 0.5f;
        
        var vertices = new VertexPositionNormalTexture[] {
            // Front face
            new(new(-halfSize, -halfSize, halfSize), Vector3.Forward, new(0, 1)),
            new(new(halfSize, -halfSize, halfSize), Vector3.Forward, new(1, 1)),
            new(new(halfSize, halfSize, halfSize), Vector3.Forward, new(1, 0)),
            new(new(-halfSize, halfSize, halfSize), Vector3.Forward, new(0, 0)),
            
            // Back face
            new(new(halfSize, -halfSize, -halfSize), Vector3.Backward, new(0, 1)),
            new(new(-halfSize, -halfSize, -halfSize), Vector3.Backward, new(1, 1)),
            new(new(-halfSize, halfSize, -halfSize), Vector3.Backward, new(1, 0)),
            new(new(halfSize, halfSize, -halfSize), Vector3.Backward, new(0, 0)),
            
            // Left face
            new(new(-halfSize, -halfSize, -halfSize), Vector3.Left, new(0, 1)),
            new(new(-halfSize, -halfSize, halfSize), Vector3.Left, new(1, 1)),
            new(new(-halfSize, halfSize, halfSize), Vector3.Left, new(1, 0)),
            new(new(-halfSize, halfSize, -halfSize), Vector3.Left, new(0, 0)),
            
            // Right face
            new(new(halfSize, -halfSize, halfSize), Vector3.Right, new(0, 1)),
            new(new(halfSize, -halfSize, -halfSize), Vector3.Right, new(1, 1)),
            new(new(halfSize, halfSize, -halfSize), Vector3.Right, new(1, 0)),
            new(new(halfSize, halfSize, halfSize), Vector3.Right, new(0, 0)),
            
            // Top face
            new(new(-halfSize, halfSize, halfSize), Vector3.Up, new(0, 1)),
            new(new(halfSize, halfSize, halfSize), Vector3.Up, new(1, 1)),
            new(new(halfSize, halfSize, -halfSize), Vector3.Up, new(1, 0)),
            new(new(-halfSize, halfSize, -halfSize), Vector3.Up, new(0, 0)),
            
            // Bottom face
            new(new(-halfSize, -halfSize, -halfSize), Vector3.Down, new(0, 1)),
            new(new(halfSize, -halfSize, -halfSize), Vector3.Down, new(1, 1)),
            new(new(halfSize, -halfSize, halfSize), Vector3.Down, new(1, 0)),
            new(new(-halfSize, -halfSize, halfSize), Vector3.Down, new(0, 0)),
        };

        var indices = new short[] {
            0, 1, 2, 0, 2, 3,       // Front
            4, 5, 6, 4, 6, 7,       // Back
            8, 9, 10, 8, 10, 11,    // Left
            12, 13, 14, 12, 14, 15, // Right
            16, 17, 18, 16, 18, 19, // Top
            20, 21, 22, 20, 22, 23  // Bottom
        };

        model.LoadFromData(vertices, indices);
    }

    private static void CreateSphere(Model3D model, float radius, int subdivisions) {
        var vertices = new List<VertexPositionNormalTexture>();
        var indices = new List<short>();

        // Generate sphere vertices using spherical coordinates
        for (int lat = 0; lat <= subdivisions; lat++) {
            var theta = lat * MathF.PI / subdivisions;
            var sinTheta = MathF.Sin(theta);
            var cosTheta = MathF.Cos(theta);

            for (int lon = 0; lon <= subdivisions; lon++) {
                var phi = lon * 2 * MathF.PI / subdivisions;
                var sinPhi = MathF.Sin(phi);
                var cosPhi = MathF.Cos(phi);

                var x = cosPhi * sinTheta;
                var y = cosTheta;
                var z = sinPhi * sinTheta;

                var position = new Vector3(x, y, z) * radius;
                var normal = Vector3.Normalize(position);
                var texCoord = new Vector2((float)lon / subdivisions, (float)lat / subdivisions);

                vertices.Add(new VertexPositionNormalTexture(position, normal, texCoord));
            }
        }

        // Generate indices
        for (int lat = 0; lat < subdivisions; lat++) {
            for (int lon = 0; lon < subdivisions; lon++) {
                var current = (short)(lat * (subdivisions + 1) + lon);
                var next = (short)(current + subdivisions + 1);

                indices.AddRange(new short[] {
                    current, next, (short)(current + 1),
                    (short)(current + 1), next, (short)(next + 1)
                });
            }
        }

        model.LoadFromData(vertices.ToArray(), indices.ToArray());
    }

    private static void CreatePlane(Model3D model, float size) {
        var halfSize = size * 0.5f;
        
        var vertices = new VertexPositionNormalTexture[] {
            new(new(-halfSize, 0, -halfSize), Vector3.Up, new(0, 0)),
            new(new(halfSize, 0, -halfSize), Vector3.Up, new(1, 0)),
            new(new(halfSize, 0, halfSize), Vector3.Up, new(1, 1)),
            new(new(-halfSize, 0, halfSize), Vector3.Up, new(0, 1)),
        };

        var indices = new short[] { 0, 1, 2, 0, 2, 3 };
        
        model.LoadFromData(vertices, indices);
    }

    private static void LoadObjModel(Model3D model, string filePath) {
        // Simple OBJ loader - this is a basic implementation
        var lines = File.ReadAllLines(filePath);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var faces = new List<(int v, int vt, int vn)>();

        foreach (var line in lines) {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0]) {
                case "v" when parts.Length >= 4:
                    vertices.Add(new Vector3(
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    ));
                    break;
                case "vn" when parts.Length >= 4:
                    normals.Add(new Vector3(
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    ));
                    break;
                case "vt" when parts.Length >= 3:
                    texCoords.Add(new Vector2(
                        float.Parse(parts[1]),
                        float.Parse(parts[2])
                    ));
                    break;
                case "f" when parts.Length >= 4:
                    // Parse face indices (v/vt/vn format)
                    for (int i = 1; i < parts.Length; i++) {
                        var indices = parts[i].Split('/');
                        faces.Add((
                            int.Parse(indices[0]) - 1,
                            indices.Length > 1 && !string.IsNullOrEmpty(indices[1]) ? int.Parse(indices[1]) - 1 : 0,
                            indices.Length > 2 && !string.IsNullOrEmpty(indices[2]) ? int.Parse(indices[2]) - 1 : 0
                        ));
                    }
                    break;
            }
        }

        // Convert to vertex array
        var vertexArray = new VertexPositionNormalTexture[faces.Count];
        var indexArray = new short[faces.Count];

        for (int i = 0; i < faces.Count; i++) {
            var face = faces[i];
            var vertex = vertices[face.v];
            var normal = face.vn < normals.Count ? normals[face.vn] : Vector3.Up;
            var texCoord = face.vt < texCoords.Count ? texCoords[face.vt] : Vector2.Zero;

            vertexArray[i] = new VertexPositionNormalTexture(vertex, normal, texCoord);
            indexArray[i] = (short)i;
        }

        model.LoadFromData(vertexArray, indexArray);
    }

    private static void LoadPlyModel(Model3D model, string filePath) {
        // Basic PLY loader implementation would go here
        Logger.Write("Model3DManager", LogLevel.Info, "PLY format not yet implemented");
    }

    private static void LoadCustomModel(Model3D model, string filePath) {
        // Custom model format loader would go here
        Logger.Write("Model3DManager", LogLevel.Info, "Custom model format not yet implemented");
    }

    /// <summary>
    /// Gets a loaded model by name
    /// </summary>
    public static Model3D? GetModel(string name) {
        return _loadedModels.TryGetValue(name, out var model) ? model : null;
    }

    /// <summary>
    /// Disposes of all loaded models
    /// </summary>
    public static void Dispose() {
        foreach (var model in _loadedModels.Values) {
            model.Dispose();
        }
        _loadedModels.Clear();
        _initialized = false;
    }

    public enum PrimitiveType {
        Cube,
        Sphere,
        Plane
    }
}