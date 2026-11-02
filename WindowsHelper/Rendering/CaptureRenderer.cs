using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using WindowsHelper.Settings;

namespace WindowsHelper.Rendering;

// Captures one window via Windows.Graphics.Capture and renders the darkened image into the overlay.
// All GPU work runs on the UI thread (owner of the overlay window) to avoid Present() deadlocks.
internal sealed class CaptureRenderer : IDisposable
{
    readonly GpuContext _gpu;
    readonly IntPtr _overlay;
    readonly SynchronizationContext _ui;
    readonly Action _firstFrame;
    readonly Action _targetClosed;
    readonly GraphicsCaptureItem _item;
    readonly Direct3D11CaptureFramePool _pool;
    readonly GraphicsCaptureSession _session;

    IDXGISwapChain1? _swap;
    ID3D11Texture2D? _copy;
    ID3D11ShaderResourceView? _copySrv;
    ID3D11Texture2D? _tiles;
    ID3D11ShaderResourceView? _tilesSrv;
    ID3D11UnorderedAccessView? _tilesUav;
    ID3D11Texture2D? _filtered;
    ID3D11ShaderResourceView? _filteredSrv;
    ID3D11UnorderedAccessView? _filteredUav;
    ID3D11Texture2D? _bgTiles;
    ID3D11ShaderResourceView? _bgTilesSrv;
    ID3D11UnorderedAccessView? _bgTilesUav;
    ID3D11Texture2D? _bgLocal;
    ID3D11ShaderResourceView? _bgLocalSrv;
    ID3D11UnorderedAccessView? _bgLocalUav;
    SizeInt32 _poolSize;
    int _width, _height, _tilesX, _tilesY;
    int _pending;
    bool _hasFrame, _firstSent, _disposed;
    string? _dumpFolder;
    ShaderParams _params;

    // Must be constructed on the UI thread; callbacks are invoked on the UI thread
    public CaptureRenderer(GpuContext gpu, IntPtr target, IntPtr overlay, AppSettings settings, Action firstFrame, Action targetClosed)
    {
        _gpu = gpu;
        _overlay = overlay;
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("CaptureRenderer needs the UI thread");
        _firstFrame = firstFrame;
        _targetClosed = targetClosed;
        SetParams(settings);

        _item = CaptureHelper.CreateItem(target);
        _item.Closed += OnItemClosed;
        _poolSize = _item.Size;
        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(gpu.WinRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _poolSize);
        _pool.FrameArrived += OnFrameArrived;
        _session = _pool.CreateCaptureSession(_item);
        TrySet(() => _session.IsCursorCaptureEnabled = false);
        TrySet(() => _session.IsBorderRequired = false); // Windows 11: no yellow capture border
        _session.StartCapture();
    }

    static void TrySet(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log.Write($"Capture option not supported: {ex.Message}"); }
    }

    void SetParams(AppSettings s)
    {
        _params.BgLevel = s.Background / 100f;
        _params.FgLevel = s.Text / 100f;
        _params.Sensitivity = s.ImageSensitivity / 100f;
        _params.ProtectImages = s.ProtectImages ? 1f : 0f;
        _params.KeepDark = s.KeepDarkAreas ? 1f : 0f;
    }

    public void UpdateSettings(AppSettings settings)
    {
        if (_disposed) return;
        SetParams(settings);
        if (_hasFrame) TryRender(); // re-render last frame with new settings
    }

    // Capture thread: only signal the UI thread, coalescing bursts of frames
    void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (Interlocked.Exchange(ref _pending, 1) == 0) _ui.Post(_ => ProcessFrames(), null);
    }

    void OnItemClosed(GraphicsCaptureItem sender, object args) => _ui.Post(_ => { if (!_disposed) _targetClosed(); }, null);

    void ProcessFrames()
    {
        Volatile.Write(ref _pending, 0);
        if (_disposed) return;

        bool first = false;
        try
        {
            // Keep only the newest frame
            Direct3D11CaptureFrame? latest = null;
            while (_pool.TryGetNextFrame() is { } frame)
            {
                latest?.Dispose();
                latest = frame;
            }
            if (latest == null) return;

            using (latest)
            {
                SizeInt32 size = latest.ContentSize;
                using (var tex = CaptureHelper.GetTexture(latest.Surface))
                {
                    var desc = tex.Description;
                    int w = Math.Min(size.Width, (int)desc.Width);
                    int h = Math.Min(size.Height, (int)desc.Height);
                    if (w <= 0 || h <= 0) return;
                    EnsureResources(w, h);
                    _gpu.Context.CopySubresourceRegion(_copy!, 0, 0, 0, 0, tex, 0, new Vortice.Mathematics.Box(0, 0, 0, w, h, 1));
                }
                _hasFrame = true;
                TryRender();

                if (size.Width != _poolSize.Width || size.Height != _poolSize.Height)
                {
                    _poolSize = size;
                    _pool.Recreate(_gpu.WinRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
                }
            }

            if (!_firstSent)
            {
                _firstSent = true;
                first = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Capture frame", ex);
        }
        if (first) _firstFrame();
    }

    void EnsureResources(int w, int h)
    {
        if (w == _width && h == _height && _swap != null) return;

        var device = _gpu.Device;
        _gpu.Context.ClearState();
        _gpu.Context.Flush();
        ReleaseTextures();

        _width = w;
        _height = h;
        _tilesX = (w + Shaders.TileSize - 1) / Shaders.TileSize;
        _tilesY = (h + Shaders.TileSize - 1) / Shaders.TileSize;

        // Full mip chain: mip 5 gives the local (32px) average brightness
        _copy = device.CreateTexture2D(new Texture2DDescription(Format.B8G8R8A8_UNorm, (uint)w, (uint)h, 1, 0,
            BindFlags.ShaderResource | BindFlags.RenderTarget, ResourceUsage.Default, CpuAccessFlags.None, 1, 0,
            ResourceOptionFlags.GenerateMips));
        _copySrv = device.CreateShaderResourceView(_copy);

        var tileDesc = new Texture2DDescription(Format.R16_Float, (uint)_tilesX, (uint)_tilesY, 1, 1,
            BindFlags.ShaderResource | BindFlags.UnorderedAccess);
        _tiles = device.CreateTexture2D(tileDesc);
        _tilesSrv = device.CreateShaderResourceView(_tiles);
        _tilesUav = device.CreateUnorderedAccessView(_tiles);
        _filtered = device.CreateTexture2D(tileDesc);
        _filteredSrv = device.CreateShaderResourceView(_filtered);
        _filteredUav = device.CreateUnorderedAccessView(_filtered);

        var bgDesc = new Texture2DDescription(Format.R16G16B16A16_Float, (uint)_tilesX, (uint)_tilesY, 1, 1,
            BindFlags.ShaderResource | BindFlags.UnorderedAccess);
        _bgTiles = device.CreateTexture2D(bgDesc);
        _bgTilesSrv = device.CreateShaderResourceView(_bgTiles);
        _bgTilesUav = device.CreateUnorderedAccessView(_bgTiles);
        _bgLocal = device.CreateTexture2D(bgDesc);
        _bgLocalSrv = device.CreateShaderResourceView(_bgLocal);
        _bgLocalUav = device.CreateUnorderedAccessView(_bgLocal);

        if (_swap == null)
        {
            var desc = new SwapChainDescription1
            {
                Width = (uint)w,
                Height = (uint)h,
                Format = Format.B8G8R8A8_UNorm,
                BufferCount = 2,
                BufferUsage = Usage.RenderTargetOutput,
                SampleDescription = new SampleDescription(1, 0),
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore,
            };
            _swap = _gpu.Factory.CreateSwapChainForHwnd(device, _overlay, desc);
            _gpu.Factory.MakeWindowAssociation(_overlay, WindowAssociationFlags.IgnoreAll);
        }
        else
        {
            _swap.ResizeBuffers(2, (uint)w, (uint)h, Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
        }
    }

    void TryRender()
    {
        try { Render(); }
        catch (Exception ex) { Log.Error("Render", ex); }
    }

    void Render()
    {
        if (_swap == null || _copySrv == null || _tilesSrv == null || _tilesUav == null ||
            _filteredSrv == null || _filteredUav == null || _bgTilesSrv == null || _bgTilesUav == null ||
            _bgLocalSrv == null || _bgLocalUav == null) return;
        var ctx = _gpu.Context;

        _params.SrcWidth = _width;
        _params.SrcHeight = _height;
        _params.TilesX = _tilesX;
        _params.TilesY = _tilesY;
        ctx.UpdateSubresource(_params, _gpu.Params);
        ctx.GenerateMips(_copySrv);

        uint groupsX = (uint)((_tilesX + 7) / 8);
        uint groupsY = (uint)((_tilesY + 7) / 8);

        // Pass 1: photo-likeness per tile
        ctx.CSSetShader(_gpu.ComputeShader);
        ctx.CSSetConstantBuffer(0, _gpu.Params);
        ctx.CSSetShaderResource(0, _copySrv);
        ctx.CSSetUnorderedAccessView(0, _tilesUav);
        ctx.CSSetUnorderedAccessView(1, _bgTilesUav);
        ctx.Dispatch(groupsX, groupsY, 1);
        ctx.ClearState();

        // Pass 2: keep only large image regions (tiles -> filtered)
        ctx.CSSetShader(_gpu.FilterShader);
        ctx.CSSetConstantBuffer(0, _gpu.Params);
        ctx.CSSetShaderResource(1, _tilesSrv);
        ctx.CSSetUnorderedAccessView(0, _filteredUav);
        ctx.Dispatch(groupsX, groupsY, 1);
        ctx.ClearState();

        // Pass 3: grow regions (filtered -> tiles)
        ctx.CSSetShader(_gpu.DilateShader);
        ctx.CSSetConstantBuffer(0, _gpu.Params);
        ctx.CSSetShaderResource(1, _filteredSrv);
        ctx.CSSetShaderResource(2, _bgTilesSrv);
        ctx.CSSetUnorderedAccessView(0, _tilesUav);
        ctx.CSSetUnorderedAccessView(1, _bgLocalUav);
        ctx.Dispatch(groupsX, groupsY, 1);
        ctx.ClearState();

        // Pass 3: smart invert into the overlay
        using var back = _swap.GetBuffer<ID3D11Texture2D>(0);
        using var rtv = _gpu.Device.CreateRenderTargetView(back);
        ctx.OMSetRenderTargets(rtv);
        ctx.RSSetViewport(0, 0, _width, _height);
        ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        ctx.VSSetShader(_gpu.VertexShader);
        ctx.PSSetShader(_gpu.PixelShader);
        ctx.PSSetConstantBuffer(0, _gpu.Params);
        ctx.PSSetShaderResource(0, _copySrv);
        ctx.PSSetShaderResource(1, _tilesSrv);
        ctx.PSSetShaderResource(2, _bgLocalSrv);
        ctx.PSSetSampler(0, _gpu.Sampler);
        ctx.Draw(3, 0);
        ctx.ClearState();

        if (_dumpFolder != null)
        {
            string folder = _dumpFolder;
            _dumpFolder = null;
            SavePng(_copy!, Path.Combine(folder, "dump-source.png"));
            SavePng(back, Path.Combine(folder, "dump-result.png"));
            Log.Write($"Dump saved: content={_width}x{_height} pool={_poolSize.Width}x{_poolSize.Height}");
        }

        _swap.Present(0, PresentFlags.None);
    }

    public void Dump(string folder)
    {
        if (_disposed || !_hasFrame) return;
        _dumpFolder = folder;
        TryRender();
    }

    unsafe void SavePng(ID3D11Texture2D source, string path)
    {
        var desc = new Texture2DDescription(Format.B8G8R8A8_UNorm, (uint)_width, (uint)_height, 1, 1,
            BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read);
        using var staging = _gpu.Device.CreateTexture2D(desc);
        var ctx = _gpu.Context;
        ctx.CopySubresourceRegion(staging, 0, 0, 0, 0, source, 0, new Vortice.Mathematics.Box(0, 0, 0, _width, _height, 1));
        var map = ctx.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            using var bmp = new Bitmap(_width, _height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var data = bmp.LockBits(new Rectangle(0, 0, _width, _height), System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            for (int y = 0; y < _height; y++)
            {
                Buffer.MemoryCopy((byte*)map.DataPointer + y * (long)map.RowPitch, (byte*)data.Scan0 + y * (long)data.Stride,
                    data.Stride, _width * 4L);
            }
            bmp.UnlockBits(data);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            ctx.Unmap(staging, 0);
        }
    }

    void ReleaseTextures()
    {
        _copySrv?.Dispose();
        _copy?.Dispose();
        _tilesUav?.Dispose();
        _tilesSrv?.Dispose();
        _tiles?.Dispose();
        _filteredUav?.Dispose();
        _filteredSrv?.Dispose();
        _filtered?.Dispose();
        _copySrv = null;
        _copy = null;
        _tilesUav = null;
        _tilesSrv = null;
        _tiles = null;
        _filteredUav = null;
        _filteredSrv = null;
        _filtered = null;
        _bgTilesUav?.Dispose();
        _bgTilesSrv?.Dispose();
        _bgTiles?.Dispose();
        _bgLocalUav?.Dispose();
        _bgLocalSrv?.Dispose();
        _bgLocal?.Dispose();
        _bgTilesUav = null;
        _bgTilesSrv = null;
        _bgTiles = null;
        _bgLocalUav = null;
        _bgLocalSrv = null;
        _bgLocal = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _item.Closed -= OnItemClosed;
        _pool.FrameArrived -= OnFrameArrived;
        try
        {
            _session.Dispose();
            _pool.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("Capture dispose", ex);
        }

        _gpu.Context.ClearState();
        ReleaseTextures();
        _swap?.Dispose();
        _swap = null;
    }
}
