using UnityEngine;
using UnityEngine.EventSystems;
using TacticalRPG.Core;
using TacticalRPG.Data;

namespace TacticalRPG.UI
{
    /// <summary>
    /// ÇANTA'da SÜRÜKLENEBİLİR eşya simgesi (çantadaki hücre ya da karakterin yuvasındaki eşya).
    /// Sürükleme durumu ve bırakma kuralı <see cref="BagInventoryView"/>'da; burası yalnız
    /// fare olaylarını ona iletir. Sağ tık: yuvadaki eşyayı çantaya geri koyar.
    /// </summary>
    public class ItemDragHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public BagInventoryView View;
        public ItemSO           Item;
        /// <summary>Eşya bir yuvadaysa sahibi (çantadaysa null).</summary>
        public CharacterCard    FromCard;
        public int              FromSlot = -1;

        public void OnBeginDrag(PointerEventData e) { if (View != null) View.BeginDrag(this, e); }
        public void OnDrag(PointerEventData e)      { if (View != null) View.Drag(e); }
        public void OnEndDrag(PointerEventData e)   { if (View != null) View.EndDrag(); }

        public void OnPointerClick(PointerEventData e)
        {
            if (View == null) return;
            if (e.button == PointerEventData.InputButton.Right && FromCard != null) View.QuickUnequip(this);
            else View.Select(Item);
        }
    }

    /// <summary>
    /// Eşyanın BIRAKILABİLECEĞİ yer: bir karakterin yuvası (<see cref="Card"/> + <see cref="Slot"/>)
    /// ya da çantanın kendisi (<see cref="Card"/> = null → yuvadan çıkar).
    /// </summary>
    public class ItemDropTarget : MonoBehaviour, IDropHandler
    {
        public BagInventoryView View;
        public CharacterCard    Card;
        public int              Slot = -1;

        public void OnDrop(PointerEventData e)
        {
            if (View == null || e.pointerDrag == null) return;
            var handle = e.pointerDrag.GetComponent<ItemDragHandle>();
            if (handle != null) View.Drop(this, handle);
        }
    }
}
