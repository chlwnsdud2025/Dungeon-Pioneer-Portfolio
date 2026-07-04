using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target; // 쫓아갈 플레이어

    [Header("Camera Offset")]
    // 플레이어로부터 카메라가 얼마나 떨어져 있을지 설정 (쿼터뷰 시점에 맞게 조절하세요)
    public Vector3 offset = new Vector3(0f, 8f, -6f);

    [Header("Smoothness")]
    public float smoothSpeed = 10f; // 카메라가 따라가는 속도

    private void LateUpdate()
    {
        if (target == null) return;

        // 목표 위치 계산 (플레이어 위치 + 오프셋)
        Vector3 desiredPosition = target.position + offset;

        // 부드럽게 이동 (Lerp)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // ※ 회전(Rotation) 코드가 없으므로, 카메라의 원래 앵글(예: X축 50도 숙임)이 그대로 유지됩니다!
    }
}