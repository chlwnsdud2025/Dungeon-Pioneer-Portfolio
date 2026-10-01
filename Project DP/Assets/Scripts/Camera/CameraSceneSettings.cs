using UnityEngine;

/// <summary>
/// 이 씬에서 GameCamera가 쓸 화면 설정. 씬에 하나 두면 씬에 들어올 때 GameCamera가 적용합니다.
/// 카메라는 씬을 넘어 하나만 쓰므로, 씬마다 달라야 하는 값만 여기에 둡니다.
/// </summary>
[DisallowMultipleComponent]
public class CameraSceneSettings : MonoBehaviour
{
    [Tooltip("빈 화면을 무엇으로 채울지. Skybox = 하늘, Solid Color = 아래 배경색")]
    public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;
    [Tooltip("Clear Flags가 Solid Color일 때의 배경색")]
    public Color backgroundColor = Color.black;
    [Tooltip("후처리(Volume의 블룸·색 보정 등)를 켤지")]
    public bool postProcessing = true;
}
