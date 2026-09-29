using UnityEngine;

/// <summary>미니맵에 얹는 표시 하나. 아이콘이 있으면 아이콘, 없으면 대체 모양(점 또는 칸)으로 그립니다.</summary>
internal struct MinimapMarker
{
    public Vector2 center;          // 픽셀 좌표
    public MinimapIconImage icon;   // 있으면 아이콘을 그리고, 없으면 아래 대체 모양으로 그립니다.
    public int radius;              // 점: 반지름, 칸: 한 변의 절반
    public Color32 color;
    public bool isTile;
    public float opacity;           // 0~1
}

/// <summary>아이콘 칸의 sizePx 크기로 줄이고 색을 곱해 둔 아이콘 픽셀. (아래가 0번 줄)</summary>
internal class MinimapIconImage
{
    public Color32[] pixels;
    public int size;
}

/// <summary>
/// 미니맵 텍스처 한 장과 그 픽셀 버퍼, 그리고 그 위에 아이콘·점·칸을 그리는 도구.
/// 던전 미니맵과 던전 밖 미니맵이 함께 씁니다. 텍스처는 늘 불투명하게 유지합니다.
/// </summary>
internal class MinimapCanvas
{
    public readonly int width;
    public readonly int height;
    public readonly Color32[] pixels;
    public readonly Texture2D texture;

    public MinimapCanvas(int width, int height)
    {
        this.width = width;
        this.height = height;
        pixels = new Color32[width * height];

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
    }

    public void Upload()
    {
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    public void Destroy() => Object.Destroy(texture);

    /// <summary>마커를 그립니다. offset만큼 옮겨 그리므로, 큰 그림의 일부를 잘라 보여줄 때 창 위치를 넘기면 됩니다.</summary>
    public void Draw(in MinimapMarker marker, Vector2 offset)
    {
        Vector2 center = marker.center - offset;

        if (marker.icon != null) DrawIcon(center, marker.icon, marker.opacity);
        else if (marker.isTile) DrawSquare(center, marker.radius, marker.color);
        else DrawDot(center, marker.radius, marker.color, marker.opacity);
    }

    public void DrawIcon(Vector2 center, MinimapIconImage icon, float opacity)
    {
        int size = icon.size;
        int left = Mathf.RoundToInt(center.x - size * 0.5f);
        int bottom = Mathf.RoundToInt(center.y - size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            int ty = bottom + y;
            if (ty < 0 || ty >= height) continue;

            for (int x = 0; x < size; x++)
            {
                int tx = left + x;
                if (tx < 0 || tx >= width) continue;

                Color32 src = icon.pixels[y * size + x];
                Composite(ty * width + tx, src, Mathf.RoundToInt(src.a * opacity));
            }
        }
    }

    public void DrawDot(Vector2 center, int radiusPx, Color32 color, float opacity)
    {
        int alpha = Mathf.RoundToInt(opacity * 255f);
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusPx));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + radiusPx));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusPx));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + radiusPx));

        float radiusSq = radiusPx * radiusPx;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - center.x;
                float dy = y + 0.5f - center.y;
                if (dx * dx + dy * dy <= radiusSq)
                    Composite(y * width + x, color, alpha);
            }
        }
    }

    public void DrawSquare(Vector2 center, int halfSize, Color32 color)
    {
        int minX = Mathf.Max(0, Mathf.RoundToInt(center.x - halfSize));
        int maxX = Mathf.Min(width, Mathf.RoundToInt(center.x + halfSize));
        int minY = Mathf.Max(0, Mathf.RoundToInt(center.y - halfSize));
        int maxY = Mathf.Min(height, Mathf.RoundToInt(center.y + halfSize));

        for (int y = minY; y < maxY; y++)
        {
            int row = y * width;
            for (int x = minX; x < maxX; x++)
                pixels[row + x] = color;
        }
    }

    /// <summary>픽셀 위에 src를 alpha(0~255)만큼 덮습니다. 결과는 늘 불투명합니다.</summary>
    private void Composite(int index, Color32 src, int alpha)
    {
        if (alpha <= 0) return;

        Color32 result = alpha >= 255 ? src : Blend(pixels[index], src, (byte)alpha);
        result.a = 255;
        pixels[index] = result;
    }

    /// <summary>from에서 to로 weight(0~255)만큼 섞습니다. 255면 to 그대로입니다.</summary>
    public static Color32 Blend(Color32 from, Color32 to, byte weight)
    {
        int w = weight, rest = 255 - weight;
        return new Color32(
            (byte)((from.r * rest + to.r * w) / 255),
            (byte)((from.g * rest + to.g * w) / 255),
            (byte)((from.b * rest + to.b * w) / 255),
            (byte)((from.a * rest + to.a * w) / 255));
    }
}
