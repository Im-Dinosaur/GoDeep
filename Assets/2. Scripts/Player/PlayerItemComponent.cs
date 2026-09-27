using UnityEngine;

namespace GoDeep
{
    public sealed class PlayerItemComponent : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingPackCount = 2; //시작 산소팩 수
        [SerializeField, Min(1)] private int maximumPackCount = 3; //최대 산소팩 수
        [SerializeField, Min(1f)] private float oxygenPerPack = 35f; //산소팩 회복량
        private int packCount; //현재 산소팩 수
        private bool itemEquipped; //아이템을 들고 있는 상태

        public int count => packCount; //현재 소지 수량
        public bool isEquipped => itemEquipped; //현재 손 상태

        private void Awake() //소지품 초기화
        {
            packCount = Mathf.Clamp(startingPackCount, 0, maximumPackCount);
        }

        public void switchHand() //빈손과 산소팩 사이 전환
        {
            itemEquipped = packCount > 0 && !itemEquipped;
        }

        public bool tryUse(Player receiver) //실제 산소 회복 성공 후에만 산소팩 한 개 소비
        {
            if (!itemEquipped || packCount <= 0 || receiver == null || !receiver.receiveOxygen(oxygenPerPack)) return false;
            packCount--;
            if (packCount == 0) itemEquipped = false;
            return true;
        }

        public bool addPack() //소지 공간이 있을 때 산소팩 한 개 획득
        {
            if (packCount >= maximumPackCount) return false;
            packCount++;
            return true;
        }
    }
}
