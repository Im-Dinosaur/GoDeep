using UnityEngine;

namespace GoDeep
{
    public sealed class PrototypeSession : MonoBehaviour
    {
        public enum RunState { Exploring, Complete, Failed }
        [SerializeField] private Player localPlayer; //현재 조작하는 다이버
        [SerializeField] private Player trainingDiver; //산소 공유를 연습할 다이버
        [SerializeField] private AirPocketComponent[] airPockets; //시제품의 에어포켓 목록
        [SerializeField] private GameFlow gameFlow; //재시작과 메뉴 이동 진입점
        [SerializeField] private GameObject[] offlineOnlyObjects; //온라인 이동 시험에서는 숨길 보급품과 로컬 표지
        private RunState runState; //현재 시제품 진행 상태
        private float elapsedTime; //일시정지를 제외한 탐사 시간
        private bool enteredWater; //입구 에어포켓에서 출발했는지 여부
        private bool networkPractice; //온라인 이동 검증 모드 여부

        public Player player => localPlayer; //UI가 사용할 플레이어 진입점
        public RunState state => runState; //결과 화면에 표시할 진행 상태
        public float elapsed => elapsedTime; //플레이 시간
        public bool isNetworkPractice => networkPractice; //온라인 전용 안내 표시 여부

        private void Awake() //네트워크 러너가 생성할 다이버와 로컬 배치를 구분
        {
            networkPractice = NetworkSession.instance != null && NetworkSession.instance.onlineRequested;
            if (!networkPractice) return;
            if (localPlayer != null) localPlayer.gameObject.SetActive(false);
            if (trainingDiver != null) trainingDiver.gameObject.SetActive(false);
            if (offlineOnlyObjects != null)
                foreach (var item in offlineOnlyObjects) //로컬 연습에서만 사용할 장면 요소
                    if (item != null) item.SetActive(false);
            localPlayer = null;
        }

        private void Start() //시작 공간을 판정하고 조작 안내 표시
        {
            if (networkPractice) return;
            refreshPocket(localPlayer);
            if (trainingDiver != null) refreshPocket(trainingDiver);
            localPlayer.setPaused(true);
        }

        private void Update() //입력과 공간과 산소를 순서대로 갱신
        {
            if (networkPractice)
            {
                localPlayer = NetworkSession.instance != null ? NetworkSession.instance.localPlayer : null;
                if (localPlayer != null)
                {
                    refreshPocket(localPlayer);
                    elapsedTime += Time.deltaTime;
                }
                return;
            }
            var input = localPlayer.readInput(); //이번 프레임의 로컬 입력
            if (input.restart)
            {
                gameFlow.restartPrototype();
                return;
            }
            if (runState != RunState.Exploring) return;
            var deltaTime = Time.deltaTime; //일시정지를 반영할 프레임 시간
            localPlayer.processInput(input, deltaTime);
            if (localPlayer.isPaused) return;
            refreshPocket(localPlayer);
            localPlayer.simulateVitals(deltaTime);
            //연습 다이버는 고정된 산소 상태로 대기하며 사용자가 공유를 시험할 시간을 확보한다.
            elapsedTime += deltaTime;
            if (!localPlayer.isInAirPocket) enteredWater = true;
            if (localPlayer.lifeState == PlayerOxygenComponent.LifeState.Eliminated) finish(RunState.Failed);
            else if (enteredWater && localPlayer.isInAirPocket && localPlayer.airPocket.destination)
                finish(RunState.Complete);
        }

        private void refreshPocket(Player target) //현재 위치로 에어포켓 소속 갱신
        {
            target.setAirPocket(null);
            foreach (var pocket in airPockets) //검사할 안전 공간
            {
                if (pocket != null && pocket.contains(target.transform.position))
                {
                    target.setAirPocket(pocket);
                    return;
                }
            }
        }

        private void finish(RunState result) //도착 또는 탈락 결과를 확정하고 조작 중단
        {
            runState = result;
            localPlayer.setPaused(true);
        }

        public void resumeDive() //시작 또는 일시정지 화면에서 수영 재개
        {
            if (runState == RunState.Exploring && localPlayer != null) localPlayer.setPaused(false);
        }
    }
}
