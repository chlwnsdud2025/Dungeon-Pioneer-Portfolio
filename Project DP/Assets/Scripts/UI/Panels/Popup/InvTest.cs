using UnityEngine;

public class InvTest : MonoBehaviour
{
    public Inventory inv;
    public ItemData potion;
    public ItemData sword;
    public ItemData shield;
    public ItemData helmet;
    public ItemData armor;

    void Start()
    {
        if (potion != null) inv.AddItem(potion, 5);
        if (sword != null)  inv.AddItem(sword, 1);
        if (shield != null) inv.AddItem(shield, 1);
        if (helmet != null) inv.AddItem(helmet, 1);
        if (armor != null)  inv.AddItem(armor, 1);
    }
}