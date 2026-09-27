using UnityEngine;

namespace GoDeep
{
    public sealed class DiveInteractableComponent : MonoBehaviour
    {
        public enum Kind { OxygenPack, RefillStation }
        [SerializeField] private Kind interactionKind; //보급품 또는 산소 공급 장치
        [SerializeField] private string interactionLabel = "OXYGEN PACK"; //조준 시 상호작용 이름
        private bool consumed; //보급품 획득 여부

        public string label => interactionLabel; //사용 안내 이름

        public bool interact(Player player) //플레이어 진입점을 통해 유효한 상호작용 적용
        {
            if (consumed || player == null) return false;
            if (interactionKind == Kind.RefillStation)
                return player.isInAirPocket && player.receiveOxygen(10000f);
            if (!player.receivePack()) return false;
            consumed = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
