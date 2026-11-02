using System.Runtime.InteropServices;

namespace WindowsHelper.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal struct ShaderParams
{
    public float SrcWidth, SrcHeight, TilesX, TilesY;
    public float BgLevel, FgLevel, Sensitivity, ProtectImages;
    public float KeepDark, Pad0, Pad1, Pad2;
}

internal static class Shaders
{
    public const int TileSize = 8;

    // CSMain:   per 8px tile: image-likeness + flat background colour (if the tile is a plain fill)
    // CSFilter: keep only large image regions (15x15 tile majority), drops icons/buttons
    // CSDilate: grow protected regions by 1 tile + average surrounding background colour
    // PSMain:   smart invert (flip luminance, keep hue); skips image pixels unless they match the background
    public const string Source = """
cbuffer Params : register(b0)
{
    float2 SrcSize;
    float2 TileCount;
    float BgLevel;
    float FgLevel;
    float Sensitivity;
    float ProtectImages;
    float KeepDark;
    float3 Pad;
};

Texture2D<float4> Src : register(t0);
Texture2D<float> TileTex : register(t1);
Texture2D<float4> BgTex : register(t2);
RWTexture2D<float> TilesOut : register(u0);
RWTexture2D<float4> BgOut : register(u1);
SamplerState Lin : register(s0);

static const float3 LumaW = float3(0.2126, 0.7152, 0.0722);

[numthreads(8, 8, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint)TileCount.x || id.y >= (uint)TileCount.y) return;

    int2 size = int2(SrcSize);
    int2 origin = int2(id.xy) * 8 - 4;
    uint hist[8] = { 0, 0, 0, 0, 0, 0, 0, 0 };
    float3 binSum[8] = { float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0),
                         float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0), float3(0, 0, 0) };
    float mid = 0.0;
    float colorful = 0.0;
    float n = 0.0;

    [loop] for (int y = 0; y < 16; y++)
    {
        [loop] for (int x = 0; x < 16; x++)
        {
            int2 p = origin + int2(x, y);
            // Outside the window: skip (clamping would repeat the 1px border many times)
            if (any(p < int2(0, 0)) || any(p >= size)) continue;
            float4 c4 = Src.Load(int3(p, 0));
            if (c4.a < 0.5) continue; // transparent rounded corners
            float3 c = c4.rgb;
            float l = dot(c, LumaW);
            float ch = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
            uint b = min((uint)(l * 8.0), 7u);
            hist[b] += 1;
            binSum[b] += c;
            mid += (l > 0.12 && l < 0.88) ? 1.0 : 0.0;
            colorful += (ch > 0.25) ? 1.0 : 0.0;
            n += 1.0;
        }
    }

    if (n < 16.0)
    {
        TilesOut[id.xy] = 0.0;
        BgOut[id.xy] = float4(0.0, 0.0, 0.0, 0.0);
        return;
    }

    uint best = 0;
    uint bestIdx = 0;
    [unroll] for (uint i = 0; i < 8; i++)
    {
        if (hist[i] > best) { best = hist[i]; bestIdx = i; }
    }

    float domFrac = best / n;
    float midFrac = mid / n;
    float colorFrac = colorful / n;
    float s = Sensitivity;

    // Image content: varied tones (photos) or colour (also flat-shaded renders); size is decided later
    float varied = 1.0 - smoothstep(0.45, 0.7, domFrac);
    float photo = smoothstep(0.55 - 0.25 * s, 0.75 - 0.25 * s, midFrac) * varied;
    float colour = smoothstep(0.45 - 0.25 * s, 0.7 - 0.25 * s, colorFrac);
    TilesOut[id.xy] = saturate(max(photo, colour));

    // Plain grey/white fill: remember its colour as background candidate
    bool isFlat = domFrac > 0.8 && colorFrac < 0.1;
    BgOut[id.xy] = isFlat ? float4(binSum[bestIdx] / best, 1.0) : float4(0.0, 0.0, 0.0, 0.0);
}

// Majority over 15x15 tiles (~120px): only large image areas survive, holes inside get filled.
// Buttons, title bars and icons are too small.
[numthreads(8, 8, 1)]
void CSFilter(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint)TileCount.x || id.y >= (uint)TileCount.y) return;

    int2 tiles = int2(TileCount);
    float count = 0.0;
    [loop] for (int dy = -7; dy <= 7; dy++)
    {
        [loop] for (int dx = -7; dx <= 7; dx++)
        {
            int2 q = int2(id.xy) + int2(dx, dy);
            if (any(q < int2(0, 0)) || any(q >= tiles)) continue; // no clamping at window edges
            count += TileTex.Load(int3(q, 0)) > 0.5 ? 1.0 : 0.0;
        }
    }
    TilesOut[id.xy] = smoothstep(90.0, 115.0, count);
}

// Grow the protected area by 1 tile; average the plain background colour within ~100px
[numthreads(8, 8, 1)]
void CSDilate(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= (uint)TileCount.x || id.y >= (uint)TileCount.y) return;

    int2 tiles = int2(TileCount);
    float m = 0.0;
    [unroll] for (int dy = -1; dy <= 1; dy++)
    {
        [unroll] for (int dx = -1; dx <= 1; dx++)
        {
            int2 q = int2(id.xy) + int2(dx, dy);
            if (any(q < int2(0, 0)) || any(q >= tiles)) continue;
            m = max(m, TileTex.Load(int3(q, 0)));
        }
    }
    TilesOut[id.xy] = m;

    float4 acc = float4(0.0, 0.0, 0.0, 0.0);
    [loop] for (int by = -6; by <= 6; by++)
    {
        [loop] for (int bx = -6; bx <= 6; bx++)
        {
            int2 q = int2(id.xy) + int2(bx, by);
            if (any(q < int2(0, 0)) || any(q >= tiles)) continue;
            float4 b = BgTex.Load(int3(q, 0));
            acc += float4(b.rgb * b.a, b.a);
        }
    }
    BgOut[id.xy] = acc.a > 0.0 ? float4(acc.rgb / acc.a, 1.0) : float4(0.0, 0.0, 0.0, 0.0);
}

float4 VSMain(uint id : SV_VertexID) : SV_Position
{
    float2 uv = float2((id << 1) & 2, id & 2);
    return float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
}

float4 PSMain(float4 pos : SV_Position) : SV_Target
{
    float4 c4 = Src.Load(int3(int2(pos.xy), 0));
    if (c4.a < 0.5) return float4(0.0, 0.0, 0.0, 1.0); // transparent corners stay dark
    float3 c = c4.rgb;

    float light = 1.0;
    if (KeepDark > 0.5)
    {
        // Average brightness of the surrounding ~32px decides: only light areas get inverted
        float local = dot(Src.SampleLevel(Lin, pos.xy / SrcSize, 5.0).rgb, LumaW);
        light = smoothstep(0.33, 0.43, local);
    }

    float image = 0.0;
    if (ProtectImages > 0.5)
    {
        float2 tuv = pos.xy / (8.0 * TileCount);
        image = TileTex.SampleLevel(Lin, tuv, 0);
        if (image > 0.0)
        {
            // Pixels with the surrounding background colour are not part of the image: invert them
            float4 bg = BgTex.SampleLevel(Lin, tuv, 0);
            if (bg.a > 0.5) image *= smoothstep(0.05, 0.12, length(c - bg.rgb / bg.a));
        }
    }

    float amount = light * (1.0 - image);
    float l = dot(c, LumaW);
    // Flip luminance with gamma: mid greys end up dark instead of staying grey
    float target = pow(saturate(1.0 - l), 1.6);
    float3 inv = saturate(c + (target - l));
    inv = BgLevel + inv * (FgLevel - BgLevel);
    return float4(lerp(c, inv, amount), 1.0);
}
""";
}
