using UnityEngine;

public class PlayerPositionSender : MonoBehaviour
{
    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPosition");

    private void Update()
    {
        // 플레이어의 발바닥이 아니라, 가슴 높이(Y축으로 +1.5만큼 위)를 구멍의 중심으로 삼습니다.
        Vector3 holeCenter = transform.position + Vector3.up * 1.5f;
        Shader.SetGlobalVector(PlayerPosID, holeCenter);
    }
}