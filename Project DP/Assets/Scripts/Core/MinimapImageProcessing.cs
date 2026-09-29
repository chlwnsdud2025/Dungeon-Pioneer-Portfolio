using UnityEngine;

/// <summary>
/// 던전 밖 미니맵에 쓰는 이미지 계산 모음. 찍은 그림을 미니맵답게 가공하고, 보여줄 창 위치를 정합니다.
/// 씬이나 컴포넌트에 기대지 않는 순수 계산이라 따로 떼어 두었습니다.
/// </summary>
internal static class MinimapImageProcessing
{
    /// <summary>
    /// 찍은 색을 미니맵답게 단순화합니다. 채도·밝기를 조절한 뒤 채널마다 levels단계로 끊습니다.
    /// 아무것도 찍히지 않은 픽셀(알파 0)은 emptyColor로 채웁니다.
    /// </summary>
    public static void Stylize(Color32[] source, Color32[] result, int levels, float saturation, float brightness, Color32 emptyColor)
    {
        int steps = Mathf.Max(1, levels - 1);

        for (int i = 0; i < source.Length; i++)
        {
            Color32 c = source[i];
            if (c.a == 0)
            {
                result[i] = emptyColor;
                continue;
            }

            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            float luminance = 0.299f * r + 0.587f * g + 0.114f * b;

            r = Quantize((luminance + (r - luminance) * saturation) * brightness, steps);
            g = Quantize((luminance + (g - luminance) * saturation) * brightness, steps);
            b = Quantize((luminance + (b - luminance) * saturation) * brightness, steps);

            result[i] = new Color32((byte)(r * 255f + 0.5f), (byte)(g * 255f + 0.5f), (byte)(b * 255f + 0.5f), 255);
        }
    }

    private static float Quantize(float value, int steps)
    {
        value = Mathf.Clamp01(value);
        return Mathf.Round(value * steps) / steps;
    }

    /// <summary>높이 셰이더가 R(상위)·G(하위)에 나눠 적은 16비트 높이를 되돌립니다. 아무것도 안 찍힌 곳은 0.</summary>
    public static ushort[] DecodeHeight(Color32[] encoded)
    {
        var heights = new ushort[encoded.Length];
        for (int i = 0; i < encoded.Length; i++)
        {
            Color32 c = encoded[i];
            heights[i] = c.a == 0 ? (ushort)0 : (ushort)(c.r << 8 | c.g);
        }

        return heights;
    }

    /// <summary>
    /// 이웃(thickness 픽셀 안)보다 threshold 이상 높은 픽셀, 즉 높은 물체의 가장자리를 외곽선 색으로 칠합니다.
    /// 나무·집·언덕이 바닥과 구분되고, 맵 바깥(높이 0)과 맞닿은 가장자리에도 선이 생깁니다.
    /// </summary>
    public static void DrawHeightOutline(ushort[] heights, int width, int height, int threshold, int thickness,
                                         Color32 outlineColor, Color32[] target)
    {
        if (thickness <= 0) return;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int h = heights[y * width + x];
                if (h == 0) continue;   // 맵 바깥에는 선을 긋지 않음

                if (HasLowerNeighbor(heights, width, height, x, y, h - threshold, thickness))
                    target[y * width + x] = outlineColor;
            }
        }
    }

    private static bool HasLowerNeighbor(ushort[] heights, int width, int height, int x, int y, int below, int thickness)
    {
        for (int dy = -thickness; dy <= thickness; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= height) continue;

            for (int dx = -thickness; dx <= thickness; dx++)
            {
                int nx = x + dx;
                if (nx < 0 || nx >= width) continue;
                if (heights[ny * width + nx] < below) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 한 축에서 보여줄 창의 시작 픽셀. 플레이어(center)를 가운데 두되 맵 끝에서는 멈춥니다.
    /// 맵이 창보다 작으면 맵이 가운데 오도록 음수를 돌려줍니다 (창 바깥은 빈 색으로 채움).
    /// </summary>
    public static int WindowStart(float center, int mapSize, int viewSize)
    {
        if (mapSize <= viewSize) return (mapSize - viewSize) / 2;

        int start = Mathf.RoundToInt(center - viewSize * 0.5f);
        return Mathf.Clamp(start, 0, mapSize - viewSize);
    }

    /// <summary>큰 그림(source)에서 (originX, originY)부터 창 크기만큼 잘라 target에 옮깁니다. 그림 밖은 emptyColor.</summary>
    public static void CopyWindow(Color32[] source, int sourceWidth, int sourceHeight, int originX, int originY,
                                  Color32[] target, int viewWidth, int viewHeight, Color32 emptyColor)
    {
        for (int y = 0; y < viewHeight; y++)
        {
            int sy = originY + y;
            int row = y * viewWidth;

            if (sy < 0 || sy >= sourceHeight)
            {
                for (int x = 0; x < viewWidth; x++) target[row + x] = emptyColor;
                continue;
            }

            // 가로로 그림 안에 들어오는 구간만 한 번에 복사하고, 양옆은 빈 색으로 채웁니다.
            int start = Mathf.Clamp(-originX, 0, viewWidth);
            int end = Mathf.Clamp(sourceWidth - originX, 0, viewWidth);

            for (int x = 0; x < start; x++) target[row + x] = emptyColor;
            if (end > start) System.Array.Copy(source, sy * sourceWidth + originX + start, target, row + start, end - start);
            for (int x = Mathf.Max(start, end); x < viewWidth; x++) target[row + x] = emptyColor;
        }
    }
}
