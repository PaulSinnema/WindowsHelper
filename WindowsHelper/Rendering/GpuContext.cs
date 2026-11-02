using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;

namespace WindowsHelper.Rendering;

// One shared D3D11 device for all overlays; used from the UI thread only
internal sealed class GpuContext : IDisposable
{
    public ID3D11Device Device { get; }
    public ID3D11DeviceContext Context { get; }
    public IDXGIFactory2 Factory { get; }
    public IDirect3DDevice WinRtDevice { get; }
    public ID3D11VertexShader VertexShader { get; }
    public ID3D11PixelShader PixelShader { get; }
    public ID3D11ComputeShader ComputeShader { get; }
    public ID3D11ComputeShader FilterShader { get; }
    public ID3D11ComputeShader DilateShader { get; }
    public ID3D11SamplerState Sampler { get; }
    public ID3D11Buffer Params { get; }

    public GpuContext()
    {
        FeatureLevel[] levels = { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };
        D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware, DeviceCreationFlags.BgraSupport, levels,
            out ID3D11Device? device, out _, out ID3D11DeviceContext? context).CheckError();
        Device = device!;
        Context = context!;

        using (var mt = Device.QueryInterfaceOrNull<ID3D11Multithread>())
            mt?.SetMultithreadProtected(true);

        using var dxgiDevice = Device.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        Factory = adapter.GetParent<IDXGIFactory2>();
        WinRtDevice = CaptureHelper.CreateWinRtDevice(dxgiDevice.NativePointer);

        VertexShader = Device.CreateVertexShader(Compile("VSMain", "vs_5_0"));
        PixelShader = Device.CreatePixelShader(Compile("PSMain", "ps_5_0"));
        ComputeShader = Device.CreateComputeShader(Compile("CSMain", "cs_5_0"));
        FilterShader = Device.CreateComputeShader(Compile("CSFilter", "cs_5_0"));
        DilateShader = Device.CreateComputeShader(Compile("CSDilate", "cs_5_0"));
        Sampler = Device.CreateSamplerState(SamplerDescription.LinearClamp);
        Params = Device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<ShaderParams>(), BindFlags.ConstantBuffer));
    }

    static byte[] Compile(string entryPoint, string profile) =>
        Compiler.Compile(Shaders.Source, entryPoint, "WindowsHelper.hlsl", profile).ToArray();

    public void Dispose()
    {
        Params.Dispose();
        Sampler.Dispose();
        DilateShader.Dispose();
        FilterShader.Dispose();
        ComputeShader.Dispose();
        PixelShader.Dispose();
        VertexShader.Dispose();
        (WinRtDevice as IDisposable)?.Dispose();
        Factory.Dispose();
        Context.Dispose();
        Device.Dispose();
    }
}
