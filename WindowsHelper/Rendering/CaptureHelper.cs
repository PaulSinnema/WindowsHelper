using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace WindowsHelper.Rendering;

// Interop between Windows.Graphics.Capture (WinRT) and Direct3D 11 (Vortice)
internal static class CaptureHelper
{
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    static Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    static Guid Texture2DIid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    public static IDirect3DDevice CreateWinRtDevice(IntPtr dxgiDevice)
    {
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out IntPtr ptr));
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(ptr);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    public static GraphicsCaptureItem CreateItem(IntPtr hwnd)
    {
        var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = factory.AsInterface<IGraphicsCaptureItemInterop>();
        IntPtr ptr = interop.CreateForWindow(hwnd, ref GraphicsCaptureItemIid);
        try
        {
            return GraphicsCaptureItem.FromAbi(ptr);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        return new ID3D11Texture2D(access.GetInterface(ref Texture2DIid));
    }
}
