using UnityEngine;

/// <summary>
/// 던전 밖 미니맵을 손으로 칠한 지도처럼 가공하는 설정. (원신 미니맵 같은 느낌)
/// </summary>
internal struct MinimapPaintStyle
{
    public int brushRadius;             // 붓질 반경(픽셀). 0이면 끔
    public float saturation;
    public float brightness;
    public Color paperColor;
    public float paperAmount;           // 종이색을 섞는 양 (0~1)
    public float sunAzimuth;            // 해가 있는 방향. 북쪽(위)에서 시계 방향 각도
    public float sunElevation;          // 해의 높이 각도 (낮을수록 음영·그림자가 길고 진함)
    public float shadeStrength;         // 높이에 따른 입체 음영 세기 (0~1)
    public float shadowStrength;        // 높은 물체가 드리우는 그림자 세기 (0~1)
    public float shadowMaxDistance;     // 그림자가 가장 길게 뻗는 거리 (월드)
    public int reliefSoftness;          // 음영·그림자를 부드럽게 퍼뜨릴 반경(픽셀)
    public float outlineStrength;       // 외곽선 자리를 얼마나 어둡게 할지 (0~1)
    public float outlineHeightThreshold;// 이웃보다 이만큼(월드) 높은 곳의 가장자리에 선
    public int outlineThickness;
    public Color32 emptyColor;
}

/// <summary>
/// 던전 밖 미니맵에 쓰는 이미지 계산 모음. 찍은 그림을 미니맵답게 가공하고, 보여줄 창 위치를 정합니다.
/// 씬이나 컴포넌트에 기대지 않는 순수 계산이라 따로 떼어 두었습니다.
///
/// 이미지 좌표: x = 월드 +X(동), y = 월드 +Z(북). 찍은 그림의 첫 줄이 남쪽 끝입니다.
/// </summary>
internal static class MinimapImageProcessing
{
    /// <summary>
    /// 찍은 색(source)과 월드 높이(heights)로 칠한 지도 그림을 만듭니다.
    /// 1) 붓질: Kuwahara 필터로 자잘한 텍스처 무늬는 뭉개고 물체 경계는 살립니다.
    /// 2) 색 보정: 채도·밝기를 조절하고 종이색을 섞습니다.
    /// 3) 입체: 높이 맵으로 햇빛 음영과 그림자를 넣고, 높은 물체 가장자리를 그 자리 색보다 어둡게 칠합니다.
    /// 아무것도 찍히지 않은 픽셀(알파 0)은 emptyColor로 채웁니다.
    /// </summary>
    public static void Paint(Color32[] source, float[] heights, int width, int height, float worldPerPixel,
                             in MinimapPaintStyle style, Color32[] result)
    {
        PaintColors(source, width, height, style, result);
        ApplyRelief(source, heights, width, height, worldPerPixel, style, result);
    }

    // ── 1·2) 붓질 + 색 보정 ─────────────────────────────────

    private static void PaintColors(Color32[] source, int width, int height, in MinimapPaintStyle style, Color32[] result)
    {
        if (style.brushRadius <= 0)
        {
            for (int i = 0; i < source.Length; i++)
                result[i] = Grade(source[i].r / 255f, source[i].g / 255f, source[i].b / 255f, source[i].a, style);
            return;
        }

        Kuwahara(source, width, height, style.brushRadius, style, result);
    }

    /// <summary>
    /// Kuwahara 필터. 픽셀을 꼭짓점으로 하는 네 칸(각 (r+1)×(r+1)) 중 색이 가장 고른 칸의 평균색을 씁니다.
    /// 경계를 넘어 섞지 않아 물감으로 칠한 듯한 그림이 됩니다. 고른 정도는 밝기가 아니라 R·G·B 분산의 합으로 봅니다.
    /// (빨간 지붕과 초록 풀처럼 밝기가 비슷한 색끼리도 섞이지 않게)
    ///
    /// 큰 맵에서도 메모리를 적게 쓰도록 맵 전체 누적합 대신, 세로 누적합을 한 줄씩 밀면서
    /// 칸 통계는 최근 r+1줄만 들고 있습니다. 그림 밖은 가장자리 픽셀을 늘린 것으로 봅니다.
    /// </summary>
    private static void Kuwahara(Color32[] source, int width, int height, int radius, in MinimapPaintStyle style, Color32[] result)
    {
        int size = radius + 1;
        int columns = width + 2 * radius;       // 열 c = 그림 x (c - radius), 양옆으로 radius만큼 늘림
        int boxes = width + radius;             // 칸 i = 왼쪽 위가 x (i - radius)인 칸, 열 i..i+radius를 덮음
        float inverseArea = 1f / (size * size);

        // 열마다 지금 칸 줄(size줄)의 R·G·B 합과 R²·G²·B² 합
        var sums = new double[6][];
        for (int k = 0; k < 6; k++) sums[k] = new double[columns];

        // 칸 줄 통계 (평균 R·G·B, 색 분산). 칸 줄 by는 그림 줄 by..by+radius를 덮습니다.
        var rows = new float[size][];
        for (int k = 0; k < size; k++) rows[k] = new float[boxes * 4];

        for (int k = 0; k < size; k++) AddRow(source, width, height, -radius + k, radius, sums, 1);

        for (int boxRow = -radius; boxRow < height; boxRow++)
        {
            if (boxRow > -radius)
            {
                AddRow(source, width, height, boxRow - 1, radius, sums, -1);
                AddRow(source, width, height, boxRow + radius, radius, sums, 1);
            }

            float[] stats = rows[(boxRow + radius) % size];
            double r = 0, g = 0, b = 0, r2 = 0, g2 = 0, b2 = 0;
            for (int c = 0; c < radius; c++)
            {
                r += sums[0][c]; g += sums[1][c]; b += sums[2][c]; r2 += sums[3][c]; g2 += sums[4][c]; b2 += sums[5][c];
            }

            for (int i = 0; i < boxes; i++)
            {
                int add = i + radius;
                r += sums[0][add]; g += sums[1][add]; b += sums[2][add]; r2 += sums[3][add]; g2 += sums[4][add]; b2 += sums[5][add];

                float meanR = (float)(r * inverseArea), meanG = (float)(g * inverseArea), meanB = (float)(b * inverseArea);
                stats[i * 4] = meanR;
                stats[i * 4 + 1] = meanG;
                stats[i * 4 + 2] = meanB;
                stats[i * 4 + 3] = (float)((r2 + g2 + b2) * inverseArea) - (meanR * meanR + meanG * meanG + meanB * meanB);

                r -= sums[0][i]; g -= sums[1][i]; b -= sums[2][i]; r2 -= sums[3][i]; g2 -= sums[4][i]; b2 -= sums[5][i];
            }

            // 그림 줄 y의 네 칸: 칸 줄 y-radius(아래쪽 두 칸)와 y(위쪽 두 칸), 각각 칸 x-radius와 x.
            int y = boxRow;
            if (y < 0) continue;

            float[] lower = rows[y % size];
            float[] upper = stats;

            for (int x = 0; x < width; x++)
            {
                float[] bestRow = lower;
                int best = x;
                float bestVariance = lower[x * 4 + 3];

                Pick(lower, x + radius, ref bestRow, ref best, ref bestVariance);
                Pick(upper, x, ref bestRow, ref best, ref bestVariance);
                Pick(upper, x + radius, ref bestRow, ref best, ref bestVariance);

                int index = y * width + x;
                result[index] = Grade(bestRow[best * 4], bestRow[best * 4 + 1], bestRow[best * 4 + 2], source[index].a, style);
            }
        }
    }

    private static void Pick(float[] row, int box, ref float[] bestRow, ref int best, ref float bestVariance)
    {
        float variance = row[box * 4 + 3];
        if (variance >= bestVariance) return;

        bestRow = row;
        best = box;
        bestVariance = variance;
    }

    private static void AddRow(Color32[] source, int width, int height, int y, int radius, double[][] sums, int sign)
    {
        int rowStart = Mathf.Clamp(y, 0, height - 1) * width;
        int columns = sums[0].Length;

        for (int c = 0; c < columns; c++)
        {
            Color32 p = source[rowStart + Mathf.Clamp(c - radius, 0, width - 1)];
            float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;

            sums[0][c] += sign * r;
            sums[1][c] += sign * g;
            sums[2][c] += sign * b;
            sums[3][c] += sign * r * r;
            sums[4][c] += sign * g * g;
            sums[5][c] += sign * b * b;
        }
    }

    private static Color32 Grade(float r, float g, float b, byte alpha, in MinimapPaintStyle style)
    {
        float luminance = 0.299f * r + 0.587f * g + 0.114f * b;
        Color paper = style.paperColor;
        float amount = Mathf.Clamp01(style.paperAmount);

        r = Mathf.Lerp((luminance + (r - luminance) * style.saturation) * style.brightness, paper.r, amount);
        g = Mathf.Lerp((luminance + (g - luminance) * style.saturation) * style.brightness, paper.g, amount);
        b = Mathf.Lerp((luminance + (b - luminance) * style.saturation) * style.brightness, paper.b, amount);

        return new Color32(ToByte(r), ToByte(g), ToByte(b), alpha);
    }

    private static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f + 0.5f);

    // ── 3) 입체: 음영·그림자·외곽선 ─────────────────────────

    private static void ApplyRelief(Color32[] source, float[] heights, int width, int height, float worldPerPixel,
                                    in MinimapPaintStyle style, Color32[] result)
    {
        float azimuth = style.sunAzimuth * Mathf.Deg2Rad;
        float elevation = Mathf.Clamp(style.sunElevation, 5f, 89f) * Mathf.Deg2Rad;
        var toSun = new Vector2(Mathf.Sin(azimuth), Mathf.Cos(azimuth));
        var light = new Vector3(toSun.x * Mathf.Cos(elevation), toSun.y * Mathf.Cos(elevation), Mathf.Sin(elevation));
        float tanElevation = Mathf.Tan(elevation);

        // 각진 지붕·언덕을 그대로 쓰면 음영이 1픽셀 띠로 끊기므로, 높이를 살짝 퍼뜨려 둥글게 만든 뒤 기울기를 봅니다.
        float[] relief = (float[])heights.Clone();
        BoxBlur(relief, width, height, style.reliefSoftness);

        float[] shadow = null;
        if (style.shadowStrength > 0f)
        {
            shadow = CastShadow(heights, width, height, worldPerPixel, toSun, tanElevation, style.shadowMaxDistance);
            BoxBlur(shadow, width, height, style.reliefSoftness + 1);
        }

        float outlineThreshold = Mathf.Max(0.0001f, style.outlineHeightThreshold);

        for (int y = 0; y < height; y++)
        {
            int down = Mathf.Max(0, y - 1), up = Mathf.Min(height - 1, y + 1);

            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (source[index].a == 0)
                {
                    result[index] = style.emptyColor;
                    continue;
                }

                int left = Mathf.Max(0, x - 1), right = Mathf.Min(width - 1, x + 1);
                float slopeX = (relief[y * width + right] - relief[y * width + left]) / (Mathf.Max(1, right - left) * worldPerPixel);
                float slopeY = (relief[up * width + x] - relief[down * width + x]) / (Mathf.Max(1, up - down) * worldPerPixel);

                // 평평한 바닥이 1이 되도록 맞춘 햇빛 밝기
                float shade = (-slopeX * light.x - slopeY * light.y + light.z) / Mathf.Sqrt(slopeX * slopeX + slopeY * slopeY + 1f) / light.z;
                float factor = 1f + style.shadeStrength * (shade - 1f);

                if (shadow != null) factor *= 1f - style.shadowStrength * shadow[index];

                if (style.outlineThickness > 0 && style.outlineStrength > 0f
                    && HasLowerNeighbor(heights, width, height, x, y, heights[index] - outlineThreshold, style.outlineThickness))
                    factor *= 1f - style.outlineStrength;

                factor = Mathf.Clamp(factor, 0f, 1.6f);

                Color32 c = result[index];
                result[index] = new Color32(ToByte(c.r / 255f * factor), ToByte(c.g / 255f * factor), ToByte(c.b / 255f * factor), 255);
            }
        }
    }

    /// <summary>
    /// 해 쪽으로 걸어가며 해를 가리는 높은 곳이 있는지 봅니다. 가려지면 1, 아니면 0.
    /// 맵이 커도 가볍도록 최대 16번만 살펴보고, 듬성듬성한 자리는 나중에 퍼뜨려 메웁니다.
    /// </summary>
    private static float[] CastShadow(float[] heights, int width, int height, float worldPerPixel,
                                      Vector2 toSun, float tanElevation, float maxDistance)
    {
        var shadow = new float[heights.Length];
        float maxPixels = maxDistance / worldPerPixel;
        if (maxPixels < 1f) return shadow;

        int samples = Mathf.Min(16, Mathf.CeilToInt(maxPixels));
        float stride = maxPixels / samples;
        float risePerPixel = worldPerPixel * tanElevation;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float ground = heights[index];

                for (int k = 1; k <= samples; k++)
                {
                    float distance = k * stride;
                    int sx = Mathf.RoundToInt(x + toSun.x * distance);
                    int sy = Mathf.RoundToInt(y + toSun.y * distance);
                    if (sx < 0 || sx >= width || sy < 0 || sy >= height) break;

                    if (heights[sy * width + sx] - ground > distance * risePerPixel)
                    {
                        shadow[index] = 1f;
                        break;
                    }
                }
            }
        }

        return shadow;
    }

    private static bool HasLowerNeighbor(float[] heights, int width, int height, int x, int y, float below, int thickness)
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

    /// <summary>가로·세로로 나눠 하는 상자 흐림. 그림 밖은 가장자리 값을 늘린 것으로 봅니다.</summary>
    public static void BoxBlur(float[] data, int width, int height, int radius)
    {
        if (radius <= 0) return;

        var line = new float[Mathf.Max(width, height)];
        float inverse = 1f / (radius * 2 + 1);

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++) line[x] = data[row + x];
            BlurLine(line, width, radius, inverse, data, row, 1);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++) line[y] = data[y * width + x];
            BlurLine(line, height, radius, inverse, data, x, width);
        }
    }

    private static void BlurLine(float[] line, int length, int radius, float inverse, float[] target, int start, int step)
    {
        float sum = 0f;
        for (int k = -radius; k <= radius; k++) sum += line[Mathf.Clamp(k, 0, length - 1)];

        for (int i = 0; i < length; i++)
        {
            target[start + i * step] = sum * inverse;
            sum += line[Mathf.Min(i + radius + 1, length - 1)] - line[Mathf.Max(i - radius, 0)];
        }
    }

    // ── 높이 ────────────────────────────────────────────────

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
    /// 16비트 높이를 월드 높이로 바꿉니다. 아무것도 안 찍힌 곳은 맵에서 가장 낮은 높이로 채워,
    /// 맵 바깥과의 경계가 낭떠러지처럼 음영·그림자·외곽선을 만들지 않게 합니다.
    /// </summary>
    public static float[] ToWorldHeights(ushort[] encoded, float heightMin, float heightRange)
    {
        var world = new float[encoded.Length];
        float scale = heightRange / 65535f;
        float lowest = float.MaxValue;

        for (int i = 0; i < encoded.Length; i++)
        {
            if (encoded[i] == 0) continue;
            world[i] = heightMin + encoded[i] * scale;
            if (world[i] < lowest) lowest = world[i];
        }

        if (lowest == float.MaxValue) lowest = heightMin;
        for (int i = 0; i < encoded.Length; i++)
        {
            if (encoded[i] == 0) world[i] = lowest;
        }

        return world;
    }

    // ── 보여줄 창 ───────────────────────────────────────────

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
