using System.Collections;
using UnityEngine;

public static class TransitionMotion
{
    /// <summary>
    /// 문 안쪽으로 걸어 들어가며 화면을 어둡게 합니다. 끝나면 화면이 완전히 까맣습니다.
    /// path가 없으면 페이드만 합니다.
    /// </summary>
    public static IEnumerator Depart(TransitionPath path)
    {
        float fade = path != null ? path.fadeDuration : 0.4f;

        StairTransitionState_Player motion = BeginMotion(path, enter: true);
        if (motion == null)
        {
            yield return ScreenFader.Instance.FadeTo(1f, fade);
            yield break;
        }

        // 꺾이는 지점을 지난 뒤부터 어두워져야 걸어 들어가는 모습이 보입니다.
        // 페이드는 이동과 동시에 진행되어야 하므로, 전역 상주 오브젝트인 ScreenFader에서 따로 돌립니다.
        yield return new WaitUntil(() => motion.ReachedFoot || motion.IsFinished);
        ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeTo(1f, fade));
        yield return new WaitUntil(() => motion.IsFinished && ScreenFader.Instance.IsOpaque);
    }

    /// <summary>
    /// 문 안쪽에서 바깥으로 걸어 나오며 화면을 밝힙니다. 끝나면 플레이어는 Idle로 돌아갑니다.
    /// path가 없으면 페이드만 합니다.
    /// </summary>
    public static IEnumerator Arrive(TransitionPath path)
    {
        float fade = path != null ? path.fadeDuration : 0.4f;

        StairTransitionState_Player motion = BeginMotion(path, enter: false);
        if (motion == null)
        {
            yield return null;
            yield return ScreenFader.Instance.FadeTo(0f, fade);
            ReturnToIdle();
            yield break;
        }

        yield return new WaitUntil(() => motion.ReachedFoot || motion.IsFinished);
        SnapCamera();
        ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeTo(0f, fade));
        yield return new WaitUntil(() => motion.IsFinished && ScreenFader.Instance.IsClear);
        ReturnToIdle();
    }

    /// <summary>도착 연출을 시작하기 전에 플레이어를 문 안쪽에 세웁니다.</summary>
    public static void PlaceAtInside(TransitionPath path)
    {
        if (path == null || !path.IsValid || PlayerManager.Instance == null) return;

        Vector3 heading = path.OutsidePosition - path.InsidePosition;
        heading.y = 0f;
        Quaternion rotation = heading.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(heading.normalized)
            : Quaternion.identity;

        PlayerManager.Instance.PlaceAt(path.InsidePosition, rotation);
    }

    private static StairTransitionState_Player BeginMotion(TransitionPath path, bool enter)
    {
        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        if (player == null || path == null || !path.IsValid) return null;

        StairTransitionState_Player motion = player.StairTransitionState;
        motion.Begin(enter ? path.EnterPath : path.ExitPath, path.moveSpeed);
        return motion;
    }

    private static void SnapCamera()
    {
        CameraFollow follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.SnapToTarget();
    }

    private static void ReturnToIdle()
    {
        PlayerController player = PlayerManager.Instance != null ? PlayerManager.Instance.Controller : null;
        if (player != null && player.stateMachine.CurrentState is StairTransitionState_Player)
            player.stateMachine.ChangeState<IdleState_Player>();
    }
}
