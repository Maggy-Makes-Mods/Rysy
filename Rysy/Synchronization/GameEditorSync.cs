using Rysy.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Rysy.Synchronization;

/// <summary>
/// Handles real-time synchronization between map editor and game
/// </summary>
public static class GameEditorSync {
    private static UdpClient? _udpClient;
    private static IPEndPoint? _gameEndpoint;
    private static bool _isListening = false;
    private static readonly object _lockObject = new();
    
    public static bool IsConnected => _gameEndpoint != null;
    public static event Action<Vector2>? OnPlayerPositionReceived;
    public static event Action<Vector2>? OnCameraPositionReceived;
    
    private const int EDITOR_PORT = 37264; // RYSY in numbers
    private const int GAME_PORT = 37265;

    public static void Initialize() {
        Logger.Write("GameEditorSync", LogLevel.Info, "Initializing game-editor synchronization");
        
        try {
            _udpClient = new UdpClient(EDITOR_PORT);
            StartListening();
        } catch (Exception ex) {
            Logger.Write("GameEditorSync", LogLevel.Warning, $"Failed to initialize sync: {ex.Message}");
        }
    }

    private static async void StartListening() {
        if (_udpClient == null) return;
        
        _isListening = true;
        Logger.Write("GameEditorSync", LogLevel.Info, $"Listening for game sync on port {EDITOR_PORT}");

        try {
            while (_isListening) {
                var result = await _udpClient.ReceiveAsync();
                ProcessMessage(result.Buffer, result.RemoteEndPoint);
            }
        } catch (Exception ex) when (_isListening) {
            Logger.Write("GameEditorSync", LogLevel.Error, $"Error in sync listener: {ex.Message}");
        }
    }

    private static void ProcessMessage(byte[] data, IPEndPoint sender) {
        try {
            var message = Encoding.UTF8.GetString(data);
            var syncData = JsonSerializer.Deserialize<SyncMessage>(message);
            
            if (syncData == null) return;

            // Update game endpoint if this is a new connection
            if (_gameEndpoint == null || !_gameEndpoint.Equals(sender)) {
                _gameEndpoint = sender;
                Logger.Write("GameEditorSync", LogLevel.Info, $"Connected to game at {sender}");
            }

            // Process the sync data
            switch (syncData.Type) {
                case "player_position":
                    if (syncData.PlayerPosition.HasValue) {
                        OnPlayerPositionReceived?.Invoke(syncData.PlayerPosition.Value);
                        UpdatePlayerInEditor(syncData.PlayerPosition.Value, syncData.RoomName);
                    }
                    break;
                case "camera_position":
                    if (syncData.CameraPosition.HasValue) {
                        OnCameraPositionReceived?.Invoke(syncData.CameraPosition.Value);
                        UpdateCameraInEditor(syncData.CameraPosition.Value);
                    }
                    break;
            }
        } catch (Exception ex) {
            Logger.Write("GameEditorSync", LogLevel.Warning, $"Failed to process sync message: {ex.Message}");
        }
    }

    private static void UpdatePlayerInEditor(Vector2 position, string? roomName) {
        if (EditorState.Map == null) return;

        // Find or switch to the correct room
        Room? targetRoom = null;
        if (!string.IsNullOrEmpty(roomName)) {
            targetRoom = EditorState.Map.TryGetRoomByName(roomName);
        }
        
        if (targetRoom == null) {
            // Find room containing this position
            foreach (var room in EditorState.Map.Rooms) {
                var roomBounds = new Rectangle(room.X, room.Y, room.Width, room.Height);
                if (roomBounds.Contains(position.ToPoint())) {
                    targetRoom = room;
                    break;
                }
            }
        }

        if (targetRoom != null && EditorState.CurrentRoom != targetRoom) {
            EditorState.CurrentRoom = targetRoom;
            Logger.Write("GameEditorSync", LogLevel.Debug, $"Switched to room: {targetRoom.Name}");
        }

        // Update player entity position if it exists
        if (targetRoom != null) {
            var playerEntity = targetRoom.Entities.FirstOrDefault(e => e.Name == "player");
            if (playerEntity != null) {
                var relativePos = position - new Vector2(targetRoom.X, targetRoom.Y);
                playerEntity.Pos = relativePos;
                targetRoom.ClearRenderCache();
            }
        }
    }

    private static void UpdateCameraInEditor(Vector2 position) {
        if (EditorState.Camera != null) {
            EditorState.Camera.Goto(position);
        }
    }

    /// <summary>
    /// Sends editor data to the game
    /// </summary>
    public static void SendToGame(string type, object data) {
        if (_udpClient == null || _gameEndpoint == null) return;

        try {
            var message = new SyncMessage {
                Type = type,
                Timestamp = DateTime.UtcNow,
                EditorData = data
            };

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            _udpClient.Send(bytes, bytes.Length, _gameEndpoint);
        } catch (Exception ex) {
            Logger.Write("GameEditorSync", LogLevel.Warning, $"Failed to send data to game: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends player position from editor to game
    /// </summary>
    public static void SendPlayerPosition(Vector2 position, string? roomName = null) {
        if (!IsConnected) return;

        SendToGame("editor_player_position", new {
            position = new { x = position.X, y = position.Y },
            roomName = roomName ?? EditorState.CurrentRoom?.Name
        });
    }

    /// <summary>
    /// Sends camera position from editor to game
    /// </summary>
    public static void SendCameraPosition(Vector2 position) {
        if (!IsConnected) return;

        SendToGame("editor_camera_position", new {
            position = new { x = position.X, y = position.Y }
        });
    }

    public static void Dispose() {
        lock (_lockObject) {
            _isListening = false;
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;
            _gameEndpoint = null;
        }
    }

    private class SyncMessage {
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public Vector2? PlayerPosition { get; set; }
        public Vector2? CameraPosition { get; set; }
        public string? RoomName { get; set; }
        public object? EditorData { get; set; }
    }
}