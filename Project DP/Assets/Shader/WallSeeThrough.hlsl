// 벽을 스크린도어(촘촘한 바둑판) 방식으로 반쯤 비치게 합니다. WallSeeThrough 셰이더 그래프의 Custom Function 노드가 부릅니다.
//
// 진짜 반투명(알파 블렌딩)을 쓰지 않는 이유: 반투명 물체는 깊이를 남기지 않아 HBAO·GI·반사·안개·Umbra 그림자 같은
// 화면 효과를 받지 못합니다. 그래서 불투명 벽과 바꿔 끼우는 순간 벽이 확 달라 보입니다. 이 방식은 끝까지 불투명으로
// 그리면서 픽셀만 걸러 그리므로 효과를 그대로 받고, 진행도 0에서는 원래 벽과 똑같습니다.
//
// 전환: 월드 위치 노이즈로 만든 경계가 벽을 훑고 지나가며(디졸브), 지나간 자리만 바둑판으로 비칩니다.
// 노이즈가 화면이 아니라 월드 위치 기준이라 카메라가 움직여도 무늬가 벽에 붙어 있습니다.
//
// 바둑판: 픽셀을 4×4 Bayer 순서로 걸러 그립니다. Fade 0.5면 정확히 한 칸씩 건너뛴 바둑판이 되어 반투명처럼 보입니다.
// 무늬는 실제로 그리는 해상도(_ScaledScreenParams) 기준입니다. 이 프로젝트는 절반 해상도로 그려 키우므로,
// 원래 화면 크기(_ScreenParams)로 계산하면 칸이 어긋나 줄무늬가 됩니다.
//
// 그림자를 그릴 때는 거르지 않아 벽이 비쳐도 바닥 그림자는 그대로입니다.
#ifndef WALL_SEE_THROUGH_INCLUDED
#define WALL_SEE_THROUGH_INCLUDED

static const float WallSeeThrough_Bayer[16] =
{
     0,  8,  2, 10,
    12,  4, 14,  6,
     3, 11,  1,  9,
    15,  7, 13,  5
};

float WallSeeThrough_Hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// 3D 값 노이즈 (0~1)
float WallSeeThrough_Noise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = WallSeeThrough_Hash(i);
    float n100 = WallSeeThrough_Hash(i + float3(1, 0, 0));
    float n010 = WallSeeThrough_Hash(i + float3(0, 1, 0));
    float n110 = WallSeeThrough_Hash(i + float3(1, 1, 0));
    float n001 = WallSeeThrough_Hash(i + float3(0, 0, 1));
    float n101 = WallSeeThrough_Hash(i + float3(1, 0, 1));
    float n011 = WallSeeThrough_Hash(i + float3(0, 1, 1));
    float n111 = WallSeeThrough_Hash(i + float3(1, 1, 1));

    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

// 블록 모서리인지. 로컬 좌표에서 세 축 중 두 축 이상이 블록 끝(HalfSize)에 EdgeWidth만큼 가까우면 모서리입니다.
// 로컬 좌표라 블록이 회전돼 있어도 맞습니다.
bool WallSeeThrough_IsEdge(float3 WorldPosition, float EdgeWidth, float HalfSize)
{
    if (EdgeWidth <= 0.0) return false;

    float3 local = abs(TransformWorldToObject(WorldPosition));
    float3 nearEnd = step(HalfSize - EdgeWidth, local);
    return nearEnd.x + nearEnd.y + nearEnd.z >= 2.0;
}

// Fade      = 다 비쳤을 때 걸러낼 비율 (0 = 그대로, 0.5 = 바둑판 반투명, 1 = 완전히 사라짐)
// Progress  = 전환 진행도 (0 = 원래 벽, 1 = 벽 전체가 Fade만큼 비침)
// PixelSize = 바둑판 한 칸이 몇 픽셀인지 (1 = 그리는 해상도의 한 픽셀)
// NoiseScale = 디졸브 무늬 크기 (클수록 잘게)
// EdgeWidth = 걸러내지 않고 남길 블록 모서리 띠의 두께 (블록 로컬 단위, 0이면 모서리도 비침)
// HalfSize  = 블록 반 크기 (블록 로컬 단위. BaseDungeonWall은 약 1)
// Alpha는 0 또는 1 (Alpha Clip Threshold 0.5로 버림).
//
// 바둑판은 모든 벽에 같은 화면 무늬라, 앞뒤로 겹친 벽도 같은 픽셀이 빠져 겹친 곳이 진해지지 않습니다.
// 그래서 벽이 한 장의 막처럼 납작해져 블록 경계·층고가 안 보이므로, 모서리 선은 남겨 벽의 모양을 알려줍니다.
void WallSeeThrough_float(float Fade, float Progress, float3 WorldPosition, float PixelSize, float NoiseScale,
                          float EdgeWidth, float HalfSize, out float Alpha)
{
    Alpha = 1.0;

#if defined(SHADERPASS) && defined(SHADERPASS_SHADOWCASTER)
#if SHADERPASS == SHADERPASS_SHADOWCASTER
    return;
#endif
#endif

    if (Fade <= 0.0 || Progress <= 0.0) return;
    if (WallSeeThrough_IsEdge(WorldPosition, EdgeWidth, HalfSize)) return;

    // 디졸브 경계가 아직 지나가지 않은 곳은 원래 벽 그대로
    if (Progress < 1.0)
    {
        float3 p = WorldPosition * NoiseScale;
        float n = WallSeeThrough_Noise(p) * 0.65 + WallSeeThrough_Noise(p * 2.7 + 13.1) * 0.35;

        // 값 노이즈는 가운데(0.5 근처)에 몰려 있어 그대로 쓰면 중간에 한꺼번에 바뀝니다. 0~1로 넓혀 고르게 지나가게 합니다.
        n = saturate((n - 0.2) / 0.6);
        if (n >= Progress) return;
    }

    if (Fade >= 1.0)
    {
        Alpha = 0.0;
        return;
    }

    float4 positionCS = TransformWorldToHClip(WorldPosition);
    float2 screenUV = positionCS.xy / positionCS.w * 0.5 + 0.5;
    uint2 pixel = (uint2)floor(screenUV * _ScaledScreenParams.xy / max(1.0, PixelSize));

    float threshold = (WallSeeThrough_Bayer[(pixel.y % 4) * 4 + pixel.x % 4] + 0.5) / 16.0;
    Alpha = threshold >= Fade ? 1.0 : 0.0;
}

#endif
