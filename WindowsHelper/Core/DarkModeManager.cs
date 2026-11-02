using WindowsHelper.Interop;
using WindowsHelper.Rendering;
using WindowsHelper.Settings;
using WindowsHelper.UI;
using static WindowsHelper.Interop.Native;

namespace WindowsHelper.Core;

internal sealed class DarkModeManager : IDisposable
{
    readonly SettingsStore _store;
    readonly WindowTracker _tracker = new();
    readonly Dictionary<IntPtr, DarkOverlay> _overlays = new();
    readonly Dictionary<IntPtr, string> _keys = new();
    readonly HashSet<IntPtr> _failed = new();
    readonly HashSet<IntPtr> _creating = new();
    readonly Dictionary<string, AppSettingsForm> _appForms = new(StringComparer.OrdinalIgnoreCase);
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 200 };
    readonly CaptionToolbar _toolbar;
    GpuContext? _gpu;
    IntPtr _active;

    public DarkModeManager(SettingsStore store)
    {
        _store = store;
        _toolbar = new CaptionToolbar(this);

        _tracker.Shown += Evaluate;
        _tracker.Restored += Evaluate;
        _tracker.CloakChanged += Evaluate;
        _tracker.Hidden += OnHidden;
        _tracker.Minimized += OnHidden;
        _tracker.Destroyed += OnDestroyed;
        _tracker.Moved += OnMoved;
        _tracker.Foreground += OnForeground;
        _store.Changed += RefreshAll;

        _timer.Tick += (_, _) => Maintain();
        _timer.Start();

        RefreshAll();
        OnForeground(GetForegroundWindow());
    }

    GpuContext Gpu => _gpu ??= new GpuContext();

    public string AppKey(IntPtr h)
    {
        if (_keys.TryGetValue(h, out var key)) return key;
        key = WindowInfo.AppKey(h);
        if (key != "applicationframehost.exe") _keys[h] = key; // UWP host may not have its app attached yet
        return key;
    }

    public bool IsDark(IntPtr target) => _store.IsDark(AppKey(target));

    // Toolbar sits above the overlay when there is one
    public IntPtr ZAnchorFor(IntPtr target) =>
        _overlays.TryGetValue(target, out var o) && o.IsShown ? o.Handle : target;

    void Evaluate(IntPtr h)
    {
        // Re-entrancy guard: WinRT calls can pump messages while an overlay is being created
        if (_failed.Contains(h) || _creating.Contains(h)) return;
        bool want = _store.Settings.Enabled && WindowInfo.IsOverlayCandidate(h) && _store.IsDark(AppKey(h));
        if (!want)
        {
            Remove(h);
            return;
        }

        var settings = _store.Effective(AppKey(h));
        if (_overlays.TryGetValue(h, out var overlay))
        {
            overlay.UpdateSettings(settings);
        }
        else
        {
            _creating.Add(h);
            try
            {
                overlay = new DarkOverlay(h, Gpu, settings, () => Remove(h));
                if (_overlays.TryGetValue(h, out var existing))
                {
                    overlay.Dispose(); // should not happen; never leave an untracked overlay
                    overlay = existing;
                }
                else
                {
                    _overlays[h] = overlay;
                }
            }
            catch (Exception ex)
            {
                _failed.Add(h);
                Log.Error($"Overlay {AppKey(h)} class={WindowInfo.ClassName(h)}", ex);
                return;
            }
            finally
            {
                _creating.Remove(h);
            }
        }
        overlay.UpdateBounds();
    }

    void Remove(IntPtr h)
    {
        if (_overlays.Remove(h, out var o)) o.Dispose();
    }

    void OnHidden(IntPtr h)
    {
        Remove(h);
        if (h == _active) _toolbar.HideBar();
    }

    void OnDestroyed(IntPtr h)
    {
        Remove(h);
        _keys.Remove(h);
        _failed.Remove(h);
        if (h == _active)
        {
            _active = IntPtr.Zero;
            _toolbar.Detach();
        }
    }

    void OnMoved(IntPtr h)
    {
        if (_overlays.TryGetValue(h, out var o)) o.UpdateBounds();
        if (h == _active) _toolbar.Reposition();
    }

    void OnForeground(IntPtr h)
    {
        Evaluate(h);
        if (_overlays.TryGetValue(h, out var fg)) fg.EnsureZOrder();

        // Toolbar only on normal captioned windows
        if (!WindowInfo.IsCandidate(h))
        {
            _active = IntPtr.Zero;
            _toolbar.Detach();
            return;
        }

        _active = h;
        if (_store.Settings.Enabled && _store.Settings.ShowToolbar) _toolbar.Attach(h);
        else _toolbar.Detach();
    }

    // Safety net for missed events and z-order changes without activation
    void Maintain()
    {
        foreach (var h in _overlays.Keys.ToList())
        {
            if (!IsWindow(h))
            {
                OnDestroyed(h);
                continue;
            }
            // Hidden/minimized/cloaked without us getting the event: drop the overlay
            if (!WindowInfo.IsOverlayCandidate(h))
            {
                Remove(h);
                continue;
            }
            var o = _overlays[h];
            o.UpdateBounds();
            o.EnsureZOrder();
        }
        _toolbar.Reposition();

        if (++_maintainTicks % 5 == 0) DestroyOrphans();
    }

    int _maintainTicks;

    // Overlay windows no longer tracked by any DarkOverlay are destroyed (safety net)
    void DestroyOrphans()
    {
        var tracked = new HashSet<IntPtr>(_overlays.Values.Select(o => o.Handle));
        var orphans = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == Environment.ProcessId && !tracked.Contains(h) && IsOwnOverlay(h)) orphans.Add(h);
            return true;
        }, IntPtr.Zero);
        foreach (var h in orphans)
        {
            Log.Write($"Orphan overlay 0x{h:X} destroyed");
            DestroyWindow(h);
        }
    }

    void RefreshAll()
    {
        foreach (var h in _overlays.Keys.ToList()) Evaluate(h);
        if (_store.Settings.Enabled)
        {
            EnumWindows((h, _) =>
            {
                if (!_overlays.ContainsKey(h)) Evaluate(h);
                return true;
            }, IntPtr.Zero);
        }

        if (_active != IntPtr.Zero && _store.Settings.Enabled && _store.Settings.ShowToolbar) _toolbar.Attach(_active);
        else _toolbar.Detach();
    }

    public void ToggleDark(IntPtr target)
    {
        if (target == IntPtr.Zero) return;
        var s = _store.GetOrCreate(AppKey(target));
        s.DarkMode = !s.DarkMode;
        _store.Update();
    }

    public void ToggleActive() => ToggleDark(_active);

    // Overlay windows are created with this exact caption
    static bool IsOwnOverlay(IntPtr h)
    {
        var sb = new System.Text.StringBuilder(64);
        return GetWindowText(h, sb, sb.Capacity) > 0 && sb.ToString() == "WindowsHelper Overlay";
    }

    // Diagnostics: log geometry and save source/result images of the active overlay
    public void DumpActive()
    {
        foreach (var (h, ov) in _overlays)
        {
            GetWindowRect(h, out RECT t);
            GetWindowRect(ov.Handle, out RECT r);
            Log.Write($"Overlay target=0x{h:X} {AppKey(h)} class={WindowInfo.ClassName(h)} isWindow={IsWindow(h)} " +
                      $"visible={IsWindowVisible(h)} cloaked={WindowInfo.IsCloaked(h)} candidate={WindowInfo.IsCandidate(h)} " +
                      $"target=({t.Left},{t.Top},{t.Width}x{t.Height}) overlay=0x{ov.Handle:X} ({r.Left},{r.Top},{r.Width}x{r.Height}) shown={ov.IsShown}");
        }
        EnumWindows((h, _) =>
        {
            if (IsOwnOverlay(h))
            {
                GetWindowRect(h, out RECT r);
                Log.Write($"Own overlay window 0x{h:X} visible={IsWindowVisible(h)} ({r.Left},{r.Top},{r.Width}x{r.Height}) tracked={_overlays.Values.Any(o => o.Handle == h)}");
            }
            return true;
        }, IntPtr.Zero);

        if (!_overlays.TryGetValue(_active, out var o))
        {
            Log.Write("Dump: no overlay on active window");
            return;
        }
        GetWindowRect(_active, out RECT wr);
        RECT fb = WindowInfo.FrameBounds(_active);
        GetWindowRect(o.Handle, out RECT ovr);
        Log.Write($"Dump {AppKey(_active)} class={WindowInfo.ClassName(_active)} " +
                  $"window=({wr.Left},{wr.Top},{wr.Width}x{wr.Height}) frame=({fb.Left},{fb.Top},{fb.Width}x{fb.Height}) " +
                  $"overlay=({ovr.Left},{ovr.Top},{ovr.Width}x{ovr.Height}) scale={WindowInfo.Scale(_active)}");
        o.Dump(AppInfo.DataFolder);
    }

    public void OpenAppSettings(string appKey, Rectangle? anchor)
    {
        if (_appForms.TryGetValue(appKey, out var form))
        {
            form.Activate();
            return;
        }
        form = new AppSettingsForm(_store, appKey);
        form.FormClosed += (_, _) => _appForms.Remove(appKey);
        _appForms[appKey] = form;
        form.ShowNear(anchor);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _tracker.Dispose();
        foreach (var f in _appForms.Values.ToList()) f.Close();
        foreach (var o in _overlays.Values) o.Dispose();
        _overlays.Clear();
        _toolbar.Dispose();
        _gpu?.Dispose();
    }
}
