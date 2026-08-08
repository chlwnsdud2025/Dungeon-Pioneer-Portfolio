using UnityEngine;

public class InvTest : MonoBehaviour
{
    public Inventory inv; public ItemData potion; public ItemData sword;
    void Start() { inv.AddItem(potion, 5); inv.AddItem(sword, 1); }
}