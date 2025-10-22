using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using Rysy.Extensions;
using Rysy.Graphics;
using Rysy.Gui;
using Rysy.Gui.Windows;
using Rysy.Helpers;
using Rysy.History;
using Rysy.Layers;
using Rysy.Loading;
using Rysy.MapAnalyzers;
using Rysy.Mods;
using Rysy.Stylegrounds;
using Rysy.Tools;

namespace Rysy.Scenes;

public sealed class EditorScene : Scene, IDisposable {
    private ToolHandler? _toolHandler;
    private readonly object _disposalLock = new();
    private volatile bool _disposed = false;
    private CancellationTokenSource? _loadingCancellationTokenSource;

    public ToolHandler? ToolHandler {
        get => _toolHandler;
        private set => _toolHandler = value;
    }

    public HistoryHandler HistoryHandler {
        get => EditorState.History ??= new HistoryHandler(Map);
        set => EditorState.History = value;
    }

    public Map? Map {
        get => EditorState.Map;
        set {
            if (EditorState.Map != value) {
                EditorState.Map = value;
                OnMapChanged();
            }
        }
    }

    private void OnMapChanged() {
        if (_disposed) return;

        var map = EditorState.Map;

        SwapMapPreserveState(map);

        // Ensure Camera is always initialized
        if (EditorState.Camera == null) {
            EditorState.Camera = new Camera();
        }

        if (map?.Rooms.Count > 0) {
            CurrentRoom = map.Rooms.First();
            CenterCameraOnRoom(CurrentRoom);
        }

        if (map is { })
            Persistence.Instance?.PushRecentMap(map);

        RysyState.OnEndOfThisFrame += GCHelper.VeryAggressiveGC;
    }

    private void SwapMapPreserveState(Map? map) {
        EditorState.Map = map;

        // history has to be cleared, as it might contain references to specific entity instances
        HistoryHandler?.Clear();

        if (HistoryHandler != null)
            HistoryHandler.Map = map;
    }

    public Room? CurrentRoom {
        get => EditorState.CurrentRoom;
        set => EditorState.CurrentRoom = value;
    }

    public Camera Camera {
        get => EditorState.Camera ??= new Camera();
        set => EditorState.Camera = value;
    }

    public EditorScene() {
        // Ensure Camera is initialized early
        if (EditorState.Camera == null) {
            EditorState.Camera = new Camera();
        }

        if (Settings.Instance?.NotificationWindowOpen == true)
            AddWindowIfNeeded<NotificationsWindow>();
    }

    public EditorScene(Map map) : this() {
        Map = map;
    }

    public EditorScene(string mapFilepath, bool fromPersistence = false, bool fromBackup = false, string? overrideFilepath = null) : this() {
        LoadMapFromBin(mapFilepath, fromPersistence, fromBackup, overrideFilepath);
    }

    public Task LoadFromPersistence() {
        if (_disposed) return Task.CompletedTask;

        if (RysyState.CmdArguments?.LoadIntoMap is { } path) {
            RysyState.CmdArguments.LoadIntoMap = null;
            return LoadMapFromBinCore(path);
        }
        
        if (!string.IsNullOrWhiteSpace(Persistence.Instance?.LastEditedMap))
            return LoadMapFromBinCore(Persistence.Instance.LastEditedMap, fromPersistence: true);
        
        Map = null;
        return Task.CompletedTask;
    }

    public override void SetupHotkeys() {
        if (_disposed) return;

        base.SetupHotkeys();

        Hotkeys?.AddHotkeyFromSettings("openMap", "ctrl+o", Open);
        Hotkeys?.AddHotkeyFromSettings("newMap", "ctrl+shift+n", () => LoadNewMap());

        HotkeysIgnoreImGui?.AddHistoryHotkeys(Undo, Redo, () => Save());

        Hotkeys?.AddHotkeyFromSettings("newRoom", "ctrl+n", AddNewRoom);

        ToolHandler?.InitHotkeys(Hotkeys);
        Camera?.CreateCameraHotkeys(Hotkeys);

        if (Hotkeys != null && ToolHandler != null)
            _ = new QuickActionHandler(Hotkeys, ToolHandler);
    }

    public void ClearMapRenderCache() {
        Map?.ClearRenderCache();
    }

    public void ChangeEditorLayer(int by) {
        if (Map is null)
            return;
        
        ClearMapRenderCache();
    }

    protected internal override void OnFileDrop(string filePath) {
        if (_disposed) return;

        base.OnFileDrop(filePath);

        if (File.Exists(filePath) && Path.GetExtension(filePath) == ".bin") {
            LoadMapFromBin(filePath);
        }
    }

    internal void LoadMapFromBin(string file, bool fromPersistence = false, bool fromBackup = false, string? overrideFilepath = null) {
        if (_disposed || !File.Exists(file)) {
            return;
        }

        // Cancel any previous loading operation
        _loadingCancellationTokenSource?.Cancel();
        _loadingCancellationTokenSource?.Dispose();
        _loadingCancellationTokenSource = new CancellationTokenSource();
        
        var token = _loadingCancellationTokenSource.Token;
        
        RysyState.OnEndOfThisFrame += () => Task.Run(async () => {
            try {
                if (token.IsCancellationRequested) return;

                if (RysyEngine.Scene is not LoadingScene) {
                    var oldScene = RysyEngine.Scene;
                    
                    RysyEngine.Scene = new LoadingScene(new([
                        new SimpleLoadTask($"Loading map {file.Censor()}", async (t) => {
                            await LoadMapFromBinCore(file, fromPersistence, fromBackup, overrideFilepath);
                            return LoadTaskResult.Success();
                        })
                    ]), onCompleted: () => {
                        if (!token.IsCancellationRequested)
                            RysyEngine.Scene = oldScene;
                    });
                    
                    return;
                }
                
                await LoadMapFromBinCore(file, fromPersistence, fromBackup, overrideFilepath);
            } catch (OperationCanceledException) {
                // Expected when cancellation is requested
            } catch (Exception ex) {
                Logger.Write("LoadMapFromBin", LogLevel.Error, $"Unexpected error in loading task: {ex}");
            }
        }, token);
    }

    internal async Task LoadMapFromBinCore(string file, bool fromPersistence = false, bool fromBackup = false, string? overrideFilepath = null, CancellationToken cancellationToken = default) {
        if (_disposed) return;

        try {
            // Just to make this run async, so we can see the loading screen.
            await Task.Delay(1, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            var mapBin = BinaryPacker.FromBinary(file);

            var map = Map.FromBinaryPackage(mapBin);
            if (overrideFilepath is { }) {
                map.Filepath = overrideFilepath;
            }
            
            if (!cancellationToken.IsCancellationRequested && !_disposed) {
                Map = map;
            }
        } catch (OperationCanceledException) {
            // Expected when cancellation is requested
        } catch (Exception e) {
            if (!_disposed) {
                Logger.Write("LoadMapFromBin", LogLevel.Error, $"Failed to load map: {e}");

                if (fromPersistence) {
                    AddWindow(new PersistenceMapLoadErrorWindow(e, "fromPersistence"));
                } else if (fromBackup) {
                    AddWindow(new PersistenceMapLoadErrorWindow(e, "fromBackup"));
                } else {
                    AddWindow(new CrashWindow("rysy.mapLoadError.other".TranslateFormatted(file.Censor()), e, (w) => {
                        if (ImGui.Button("rysy.ok".Translate())) {
                            w.RemoveSelf();
                        }
                    }));
                }
            }
        }
    }

    public void LoadNewMap(string? packageName = null) {
        if (_disposed) return;

        if (packageName is null) {
            AddNewMapWindow();
            return;
        }

        Map = Map.NewMap(packageName);
    }

    private void AddNewMapWindow() {
        if (_disposed) return;

        var wData = "";
        var window = new ScriptedWindow("New map", (w) => {
            ImGui.TextWrapped("Please enter the Package Name for your map.");
            ImGui.TextWrapped("This name is only used internally, and is not visible in-game.");
            ImGui.InputText("Package Name", ref wData, 512);
        }, 
        bottomBarFunc: (w) => {
            ImGui.BeginDisabled(string.IsNullOrWhiteSpace(wData));
            if (ImGui.Button("Create Map")) {
                LoadNewMap(wData);
                w.RemoveSelf();
            }
            ImGui.EndDisabled();
        },
        size: new(350, ImGui.GetTextLineHeightWithSpacing() * 10));

        AddWindow(window);
    }

    public void Save(bool saveAs = false) {
        if (_disposed || Map is not { })
            return;

        if (saveAs || string.IsNullOrWhiteSpace(Map.Filepath) || !File.Exists(Map.Filepath)) {
            if (!FileDialogHelper.TrySave("bin", out var filepath)) {
                return;
            }

            Map.Filepath = filepath;
            Persistence.Instance?.PushRecentMap(Map);
        }

        BackupHandler.Backup(Map);

        var analyzerCtx = MapAnalyzerRegistry.Global.Analyze(Map);

        if (analyzerCtx.Results.Any(r => r.Level == LogLevel.Error)) {
           NotificationsWindow.AddNotification(new MapAnalyzerErrorsNotification());
        }
        
        ForceSave();
    }

    private void ForceSave() {
        if (_disposed || Map is null) return;

        using var watch = new ScopedStopwatch("Saving");
        var pack = Map.IntoBinary();
        BinaryPacker.SaveToFile(pack, pack.Filename!);
        ModRegistry.NotifyFileCreatedAtRealPath(pack.Filename);
        Map.Filepath = pack.Filename;
        
        NotificationsWindow.AddNotification(new MapSavedNotification());
    }

    public void Open() {
        if (_disposed) return;

        if (FileDialogHelper.TryOpen("bin", out var path)) {
            LoadMapFromBin(path);
        }
    }

    public void AddNewRoom() {
        if (_disposed || Map is not { })
            return;

        var current = CurrentRoom;
        var room = new Room(Map, current?.Width ?? 40 * 8, current?.Height ?? 23 * 8);
        if (current is { }) {
            room.X = current.X;
            room.Y = current.Y;
            room.Name = current.Name;
        }

        room.Entities.Add(EntityRegistry.Create(new("player") {
            Attributes = new() {
                ["x"] = 20 * 8,
                ["y"] = 12 * 8,
            },
        }, room, false));
        AddWindow(new RoomEditWindow(room, newRoom: true));
    }

    public void MoveCurrentRoom(int tilesX, int tilesY) {
        if (_disposed || CurrentRoom is not { } room)
            return;

        HistoryHandler?.ApplyNewAction(new RoomMoveAction(room, tilesX, tilesY));
    }

    public void Undo() {
        if (!_disposed)
            HistoryHandler?.Undo();
    }

    public void Redo() {
        if (!_disposed)
            HistoryHandler?.Redo();
    }

    public void QuickReload() {
        if (_disposed || Map is not { } map) {
            return;
        }

        var selectedRoomName = CurrentRoom?.Name;

        Map = Map.FromBinaryPackage(map.IntoBinary());

        if (selectedRoomName is { }) {
            CurrentRoom = Map?.TryGetRoomByName(selectedRoomName);
            if (CurrentRoom != null)
                CenterCameraOnRoom(CurrentRoom);
        }
    }

    public override void Update() {
        if (_disposed) return;

        base.Update();

        if (RysyState.ImGuiAvailable) {
            ImGuiIOPtr io = ImGui.GetIO();
            // Intentionally don't check for capturing keyboard, works weirdly with search bar, and can result in seemingly eaten inputs.
            if (io.WantCaptureMouse /*|| io.WantCaptureKeyboard*/) {
                return;
            }
        }
        
        AddWindowIfNeeded<EditorGroupWindow>();

        if (Map is { }) {
            Camera?.HandleMouseMovement(Input.Global);

            HandleRoomSwapInputs(Input.Global);

            ToolHandler?.Update(Camera, CurrentRoom);
        }
    }

    private void HandleRoomSwapInputs(Input input) {
        if (_disposed || Map is not { } || ToolHandler?.CurrentTool?.AllowSwappingRooms != true || !input.Mouse.Left.Clicked()) {
            return;
        }

        var mousePos = input.Mouse.Pos.ToVector2();
        foreach (var room in Map.Rooms) {
            if (room == CurrentRoom)
                continue;

            var pos = room.WorldToRoomPos(Camera, mousePos);
            if (room.IsInBounds(pos)) {
                CurrentRoom = room;
                
                // Sync room change to game and play room music
                Rysy.Audio.EditorAudioManager.PlayRoomMusic(CurrentRoom);
                Rysy.Synchronization.GameEditorSync.SendCameraPosition(Camera.Pos);

                input.Mouse.ConsumeLeft();
                break;
            }
        }
    }

    public override void Render() {
        if (_disposed) return;

        base.Render();

        var windowSize = RysyState.Window?.ClientBounds.Size() ?? new Microsoft.Xna.Framework.Point(800, 600);
        if (Map is not { }) {
            var height = 4 * 6;
            var center = windowSize.Y / 2;

            GFX.BeginBatch();
            PicoFont.Print("No map loaded.", new Rectangle(0, center - 32, windowSize.X, height), Color.LightSkyBlue, scale: 4f);
            PicoFont.Print("Please drop a .bin", new Rectangle(0, center, windowSize.X, height), Color.White, scale: 4f);
            PicoFont.Print("file onto this window", new Rectangle(0, center + 32, windowSize.X, height), Color.White, scale: 4f);
            GFX.EndBatch();
            return;
        }
        
        using var buffer = RenderTargetPool.Get(windowSize.X, windowSize.Y);
        var gd = GFX.Batch?.GraphicsDevice;
        if (gd == null) return;

        gd.SetRenderTarget(buffer.Target);
        gd.Clear(Color.Black);
        
        var renderStylegrounds = Settings.Instance?.StylegroundPreview ?? false;
        if ((Settings.Instance?.OnlyRenderStylesAtRealScale ?? false) && Camera.Scale != 6f) {
            renderStylegrounds = false;
        }
        var fgInFront = Settings.Instance?.RenderFgStylegroundsInFront ?? false;

        if (renderStylegrounds && CurrentRoom is { }) {
            var layers = fgInFront ? StylegroundRenderer.Layers.BG : StylegroundRenderer.Layers.BGAndFG;
            StylegroundRenderer.Render(CurrentRoom, Map.Style, Camera, layers, filter: StylegroundRenderer.NotMasked);
        }
        
        foreach (var room in Map.Rooms) {
            if (room != CurrentRoom)
                room.Render(Camera, selected: false, Colorgrade.None);
        }

        if (CurrentRoom is { }) {
            CurrentRoom.Render(Camera, selected: true, Colorgrade.None);

            if (renderStylegrounds && fgInFront)
                StylegroundRenderer.Render(CurrentRoom, Map.Style, Camera, StylegroundRenderer.Layers.FG, filter: StylegroundRenderer.NotMasked);
        }
        
        gd.SetRenderTarget(null);
        GFX.BeginBatch(new SpriteBatchState(Effect: EditorState.CurrentColograde?.Set()));
        GFX.Batch?.Draw(buffer.Target, Vector2.Zero, Color.White);
        GFX.EndBatch();
        
        GFX.BeginBatch(Camera);
        foreach (var room in Map.Rooms) {
            // Darken the room if it's not selected
            if (room != CurrentRoom)
                ISprite.Rect(room.Bounds, Color.Black * .75f).Render();

            // draw the colored border around the room
            room.GetBorderSprite(Camera.Scale).Render();
        }
        GFX.EndBatch();
        
        ToolHandler?.Render(Camera, CurrentRoom);

        var input = Input.Global;

        if (input?.Keyboard?.Ctrl() == true) {
            // Reload everything
            if (input.Keyboard.IsKeyClicked(Keys.F5)) {
                RysyEngine.QueueReload();
            }
        } else if (input?.Keyboard != null) {
            // clear render cache
            if (input.Keyboard.IsKeyClicked(Keys.F4)) {
                foreach (var room in Map.Rooms)
                    room.ClearRenderCache();
            }

            // Reload textures
            if (input.Keyboard.IsKeyClicked(Keys.F5)) {
                GFX.Atlas?.DisposeTextures();
                foreach (var room in Map.Rooms)
                    room.ClearRenderCache();
                GC.Collect(3);
            }

            // Re-register entities
            if (input.Keyboard.IsKeyClicked(Keys.F6)) {
                EntityRegistry.RegisterAsync().AsTask().Wait();
                CurrentRoom?.ClearRenderCache();
                GC.Collect(3);
            }
        }
    }

    public override void RenderImGui() {
        if (_disposed) return;

        base.RenderImGui();

        Menubar.Render(this);
        RoomList.Render(this, Input.Global);
        ToolHandler?.RenderGui();
    }

    public void CenterCameraOnRoom(Room room) {
        if (_disposed || room == null) return;

        Camera?.CenterOnRealPos(room.Bounds.Center.ToVector2());
        
        // Sync camera position to game
        Rysy.Synchronization.GameEditorSync.SendCameraPosition(Camera?.Pos ?? Vector2.Zero);
        
        // Play room music if enabled
        Rysy.Audio.EditorAudioManager.PlayRoomMusic(room);
    }

    public override void OnEnd() {
        base.OnEnd();

        // Unsubscribe from events to prevent memory leaks
        EditorState.OnMapChanged -= OnMapChanged;
        
        Dispose();
    }

    public override void OnBegin() {
        if (_disposed) return;

        ToolHandler = new ToolHandler(HistoryHandler, Input.Global).UsePersistence(true);
        EditorState.OnMapChanged += OnMapChanged;

        OnMapChanged();

        base.OnBegin();
    }

    public void Dispose() {
        lock (_disposalLock) {
            if (_disposed) return;
            _disposed = true;

            // Cancel any ongoing loading operations
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource?.Dispose();
            _loadingCancellationTokenSource = null;

            // Unload tool handler
            ToolHandler?.Unload();
            ToolHandler = null;

            // Unsubscribe from events
            EditorState.OnMapChanged -= OnMapChanged;
        }
    }
}
