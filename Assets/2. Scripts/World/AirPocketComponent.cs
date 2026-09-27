using UnityEngine;

namespace GoDeep
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AirPocketComponent : MonoBehaviour
    {
        [SerializeField] private int pocketId; //에어포켓 식별자
        [SerializeField] private string pocketName = "ENTRY POCKET"; //화면에 표시할 공간 이름
        [SerializeField] private bool isDestination; //이번 시제품의 도착 지점 여부
        private BoxCollider pocketBounds; //안전 공간 판정 범위

        public int id => pocketId; //음성 구역 확장에 사용할 식별자
        public string displayName => pocketName; //표시용 이름
        public bool destination => isDestination; //완주 지점 여부

        private void Awake() //공간 판정용 콜라이더 연결
        {
            pocketBounds = GetComponent<BoxCollider>();
        }

        public bool contains(Vector3 position) //입출입 이벤트 없이 현재 좌표로 안전 공간 판정
        {
            if (pocketBounds == null) pocketBounds = GetComponent<BoxCollider>();
            var localPosition = transform.InverseTransformPoint(position) - pocketBounds.center; //영역 중심 기준 위치
            var halfSize = pocketBounds.size * 0.5f; //영역의 절반 크기
            return Mathf.Abs(localPosition.x) <= halfSize.x && Mathf.Abs(localPosition.y) <= halfSize.y
                && Mathf.Abs(localPosition.z) <= halfSize.z;
        }
    }
}
