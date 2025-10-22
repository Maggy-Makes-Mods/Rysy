using Rysy.Helpers;
using Rysy.Mods;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Rysy.Audio;

/// <summary>
/// Handles music and audio playback in the map editor
/// </summary>
public static class EditorAudioManager {
    private static bool _initialized = false;
    private static SoundEffect? _currentMusic;
    private static SoundEffectInstance? _currentMusicInstance;
    private static readonly Dictionary<string, SoundEffect> _loadedMusic = new();
    
    public static void Initialize() {
        if (_initialized) return;
        
        Logger.Write("EditorAudioManager", LogLevel.Info, "Initializing editor audio manager");
        _initialized = true;
    }

    /// <summary>
    /// Plays music for the current room if music is enabled in settings
    /// </summary>
    public static void PlayRoomMusic(Room? room) {
        if (!Settings.Instance.EnableMusicInEditor || room?.Map is null) {
            StopMusic();
            return;
        }

        var musicTrack = GetRoomMusicTrack(room);
        if (string.IsNullOrEmpty(musicTrack)) {
            StopMusic();
            return;
        }

        PlayMusic(musicTrack);
    }

    /// <summary>
    /// Gets the music track that should play for a given room
    /// </summary>
    private static string? GetRoomMusicTrack(Room room) {
        // Helper method to find music track in entity collection
        static string? FindMusicTrackInEntities(IEnumerable<Entity> entities) {
            return entities
                .Where(e => e.Name == "musicTrigger")
                .Select(e => e.EntityData.TryGetValue("track", out var track) ? track?.ToString() : null)
                .FirstOrDefault(track => !string.IsNullOrEmpty(track));
        }

        // Check for music triggers in the room entities
        var trackFromEntities = FindMusicTrackInEntities(room.Entities);
        if (!string.IsNullOrEmpty(trackFromEntities)) {
            return trackFromEntities;
        }

        // Check for music triggers in room triggers
        var trackFromTriggers = FindMusicTrackInEntities(room.Triggers);
        if (!string.IsNullOrEmpty(trackFromTriggers)) {
            return trackFromTriggers;
        }

        // Fall back to map metadata music
        return room.Map?.Meta?.Mode?.AudioState?.Music;
    }

    /// <summary>
    /// Plays a specific music track
    /// </summary>
    public static void PlayMusic(string trackName) {
        try {
            if (!_loadedMusic.TryGetValue(trackName, out var music)) {
                music = LoadMusicTrack(trackName);
                if (music != null) {
                    _loadedMusic[trackName] = music;
                }
            }

            if (music != null) {
                StopMusic();
                _currentMusic = music;
                _currentMusicInstance = music.CreateInstance();
                _currentMusicInstance.IsLooped = true;
                _currentMusicInstance.Volume = Settings.Instance.EditorMusicVolume;
                _currentMusicInstance.Play();
                
                Logger.Write("EditorAudioManager", LogLevel.Debug, $"Playing music track: {trackName}");
            }
        } catch (Exception ex) {
            Logger.Write("EditorAudioManager", LogLevel.Warning, $"Failed to play music track '{trackName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Stops currently playing music
    /// </summary>
    public static void StopMusic() {
        if (_currentMusicInstance != null) {
            _currentMusicInstance.Stop();
            _currentMusicInstance.Dispose();
            _currentMusicInstance = null;
        }
        _currentMusic = null;
    }

    /// <summary>
    /// Updates music volume based on settings
    /// </summary>
    public static void UpdateVolume() {
        if (_currentMusicInstance != null) {
            _currentMusicInstance.Volume = Settings.Instance.EditorMusicVolume;
        }
    }

    /// <summary>
    /// Loads a music track from game assets
    /// </summary>
    private static SoundEffect? LoadMusicTrack(string trackName) {
        try {
            // Try to load from Celeste's audio files
            var celesteDir = Profile.Instance?.CelesteDirectory;
            if (string.IsNullOrEmpty(celesteDir)) return null;

            var audioPath = Path.Combine(celesteDir, "Content", "Audio", "music");
            if (!Directory.Exists(audioPath)) return null;

            // Look for the track file (various formats)
            var extensions = new[] { ".ogg", ".wav", ".mp3", ".xnb" };
            foreach (var ext in extensions) {
                var filePath = Path.Combine(audioPath, $"{trackName}{ext}");
                if (File.Exists(filePath)) {
                    // For now, return null as we'd need proper audio loading implementation
                    Logger.Write("EditorAudioManager", LogLevel.Debug, $"Found music file: {filePath}");
                    return null; // TODO: Implement proper audio file loading
                }
            }

            Logger.Write("EditorAudioManager", LogLevel.Debug, $"Music track not found: {trackName}");
            return null;
        } catch (Exception ex) {
            Logger.Write("EditorAudioManager", LogLevel.Warning, $"Failed to load music track '{trackName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Disposes of all loaded audio resources
    /// </summary>
    public static void Dispose() {
        StopMusic();
        
        foreach (var music in _loadedMusic.Values) {
            music?.Dispose();
        }
        _loadedMusic.Clear();
        
        _initialized = false;
    }
}