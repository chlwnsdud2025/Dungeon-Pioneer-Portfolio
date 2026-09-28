using UnityEngine;

/// <summary>
/// 말을 걸면 머리 위 말풍선으로 한 줄씩 보여주는 모듈.
/// 이게 붙어 있지 않으면 말을 걸 수 없는 일반 NPC입니다.
///
/// 화면 팝업과 달리 플레이어의 조작을 막지 않습니다. 게임플레이 입력이 살아 있으므로
/// F를 다시 누르면 다음 줄로 넘어가고, 멀어지면 대화가 저절로 끝납니다.
/// </summary>
public class DialogueModule : NpcInteractionModule
{
    [Tooltip("이 NPC가 할 말. 비어 있으면 말을 걸 수 없습니다.")]
    [SerializeField] private DialogueData dialogue;

    [Tooltip("머리 위 말풍선. 비우면 자식에서 자동으로 찾습니다.")]
    [SerializeField] private SpeechBubble bubble;

    [Tooltip("이 거리보다 멀어지면 대화가 끝납니다. " +
             "PlayerController.interactRange(1.5)보다 넉넉히 커야 첫 줄이 뜨자마자 닫히지 않습니다.")]
    [SerializeField, Min(0f)] private float endDistance = 4f;

    [Tooltip("말을 걸면 NPC가 플레이어 쪽을 바라볼지.")]
    [SerializeField] private bool facePlayer = true;

    private NpcActor actor;
    private PlayerController talker;
    private int index = -1;

    public bool IsTalking => talker != null;

    public override bool IsEngaged => IsTalking;

    private void Awake()
    {
        actor = GetComponent<NpcActor>();
        if (bubble == null) bubble = GetComponentInChildren<SpeechBubble>(true);

        if (bubble == null)
            Debug.LogWarning($"[DialogueModule] '{name}'에 SpeechBubble이 없어 대사가 보이지 않습니다. " +
                             "말풍선 프리팹을 자식으로 넣어주세요.", this);
    }

    private void Update()
    {
        if (talker == null) return;

        // 대화가 끝나는 조건. 포커스가 아니라 거리로 보는 이유는, 고개를 잠깐 돌리는 것만으로
        // 대화가 끊기면 NPC 주위를 돌 수도 없기 때문입니다.
        if (!talker.IsAlive
            || (actor != null && !actor.IsAlive)
            || ActorMath.HorizontalDistance(transform.position, talker.transform.position) > endDistance)
        {
            EndTalk();
        }
    }

    // 층이 비활성화되거나 NPC가 사라질 때 말풍선이 켜진 채로 남지 않게 합니다.
    private void OnDisable() => EndTalk();

    public override bool CanInteract(PlayerController player) => dialogue != null && dialogue.Count > 0;

    /// <summary>F를 처음 누르면 대화 시작, 다시 누르면 다음 줄입니다.</summary>
    public override void Interact(PlayerController player)
    {
        if (talker != player) StartTalk(player);
        else Advance();
    }

    private void StartTalk(PlayerController player)
    {
        talker = player;
        index = -1;

        if (facePlayer && actor != null)
            actor.FaceToward(player.transform.position);

        Advance();
    }

    private void Advance()
    {
        index++;

        if (dialogue == null || index >= dialogue.Count)
        {
            EndTalk();
            return;
        }

        if (bubble != null) bubble.Show(dialogue.Lines[index]);
    }

    private void EndTalk()
    {
        talker = null;
        index = -1;
        if (bubble != null) bubble.Hide();
    }
}
