using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    public bool isOpen = false;

    public Animator animator;
    // OpenChestState_Player에서 이 함수를 호출할 예정입니다.
    public void Open()
    {
        isOpen = true;
        Debug.Log("상자가 열렸습니다! 아이템 획득!");
        animator.SetTrigger("OpenTrigger");
    }
}