using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어를 가릴 수 있는 벽 칸의 범위를 정합니다. 플레이어 칸에서 카메라 쪽으로 앞 depth줄,
/// 줄마다 좌우 halfWidth칸까지입니다. 플레이어가 서 있는 줄은 넣지 않습니다 (옆 벽은 플레이어를 가리지 않음).
/// 씬이나 컴포넌트에 기대지 않는 순수 계산입니다.
/// </summary>
internal static class WallOcclusionRegion
{
    /// <summary>
    /// 바닥에서 본 카메라 쪽 방향을 동서남북 중 가장 가까운 칸 방향으로 맞춥니다.
    /// 카메라가 바로 아래를 보면 방향이 없으므로 남쪽(-Z)으로 봅니다.
    /// </summary>
    public static Vector2Int TowardCamera(Vector3 cameraForward)
    {
        float x = -cameraForward.x, z = -cameraForward.z;
        if (Mathf.Abs(x) < 0.0001f && Mathf.Abs(z) < 0.0001f) return new Vector2Int(0, -1);

        if (Mathf.Abs(x) > Mathf.Abs(z)) return new Vector2Int(x > 0f ? 1 : -1, 0);
        return new Vector2Int(0, z > 0f ? 1 : -1);
    }

    /// <summary>범위 안의 칸들을 result에 담습니다. 맵 밖 칸도 담기므로 찾는 쪽에서 걸러야 합니다.</summary>
    public static void Collect(Vector2Int playerCell, Vector2Int towardCamera, int depth, int halfWidth, List<Vector2Int> result)
    {
        result.Clear();
        var side = new Vector2Int(-towardCamera.y, towardCamera.x);

        for (int along = 1; along <= depth; along++)
        {
            for (int lateral = -halfWidth; lateral <= halfWidth; lateral++)
                result.Add(playerCell + towardCamera * along + side * lateral);
        }
    }
}
