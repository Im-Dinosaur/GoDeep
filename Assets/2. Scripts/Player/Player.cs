using UnityEngine;

namespace GoDeep
{
    [DisallowMultipleComponent]
    public sealed class Player : MonoBehaviour
    {
        [SerializeField] private string diverName = "DIVER 01"; //다이버 표시 이름
        [SerializeField] private bool locallyControlled = true; //로컬 조작 대상 여부
        [SerializeField] private Camera viewCamera; //조준과 시점에 사용할 카메라
        [SerializeField] private PlayerInputComponent inputComponent; //입력 구성 요소
        [SerializeField] private PlayerMovementComponent movementComponent; //수영 구성 요소
        [SerializeField] private PlayerOxygenComponent oxygenComponent; //산소 구성 요소
        [SerializeField] private PlayerItemComponent itemComponent; //소지품 구성 요소
        [SerializeField] private PlayerWatchComponent watchComponent; //워치 구성 요소
        [SerializeField] private PlayerPresentationComponent presentationComponent; //팔과 효과음 구성 요소
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f; //상호작용 거리
        [SerializeField] private float seaSurfaceHeight = 8f; //깊이 측정 해수면 높이
        private AirPocketComponent currentPocket; //현재 안전 구역
        private Player aimedPlayer; //현재 조준한 동료
        private DiveInteractableComponent aimedInteractable; //현재 조준한 보급 대상
        private string notification = ""; //잠깐 표시할 행동 결과
        private float notificationTime; //행동 결과 표시 시간
        private bool swimmingFast; //실제 빠른 수영 입력 여부
        private bool paused; //로컬 시제품 일시정지 상태
        private bool oxygenWarningPlayed; //이번 산소 부족 경고 재생 여부

        public string displayName => diverName; //UI에 전달할 이름
        public float oxygenPercent => oxygenComponent.oxygenPercent; //UI에 전달할 산소 잔량
        public float depth => Mathf.Max(0f, seaSurfaceHeight - transform.position.y); //현재 수면 기준 깊이
        public int packCount => itemComponent != null ? itemComponent.count : 0; //소지 산소팩 수
        public bool hasEquippedItem => itemComponent != null && itemComponent.isEquipped; //아이템을 든 상태
        public bool isInAirPocket => currentPocket != null; //에어포켓 내부 여부
        public bool isPaused => paused; //일시정지 여부
        public AirPocketComponent airPocket => currentPocket; //세션에서 확인할 도착 공간
        public PlayerOxygenComponent.LifeState lifeState => oxygenComponent.state; //UI와 진행 판정용 생존 상태
        public float rescueTime => oxygenComponent.remainingRescueTime; //구조 제한 시간
        public string message => notificationTime > 0f ? notification : ""; //현재 표시할 행동 결과
        public float viewPitch => movementComponent != null ? movementComponent.viewPitch : 0f; //네트워크 전신 표현용 상하 시선

        private void Awake() //각 기능 참조 초기화
        {
            if (inputComponent == null) inputComponent = GetComponent<PlayerInputComponent>();
            if (movementComponent == null) movementComponent = GetComponent<PlayerMovementComponent>();
            if (oxygenComponent == null) oxygenComponent = GetComponent<PlayerOxygenComponent>();
            if (itemComponent == null) itemComponent = GetComponent<PlayerItemComponent>();
            if (watchComponent == null) watchComponent = GetComponent<PlayerWatchComponent>();
            if (presentationComponent == null) presentationComponent = GetComponent<PlayerPresentationComponent>();
        }

        public PlayerInputComponent.Frame readInput() //외부 갱신 루프에서 입력 프레임 조회
        {
            return inputComponent != null ? inputComponent.readInput() : default;
        }

        public void processInput(PlayerInputComponent.Frame input, float deltaTime) //입력을 담당 구성 요소에 순서대로 전달
        {
            if (!locallyControlled) return;
            if (input.pause)
            {
                setPaused(!paused);
                return;
            }
            if (paused) return;
            notificationTime = Mathf.Max(0f, notificationTime - deltaTime);
            var canAct = lifeState == PlayerOxygenComponent.LifeState.Active; //행동 가능 여부
            movementComponent.simulate(input, deltaTime, canAct);
            swimmingFast = canAct && input.fast && (input.movement.sqrMagnitude > 0.01f || Mathf.Abs(input.vertical) > 0.01f);
            updateAim();
            if (canAct && input.switchHand) itemComponent.switchHand();
            watchComponent.setVisible(input.watch && !itemComponent.isEquipped);
            if (watchComponent.selectSignal(input.signal))
            {
                notify("Signal preview only - multiplayer comes next");
                presentationComponent.playFeedback();
            }
            if (!canAct) return;
            if (itemComponent.isEquipped && (input.selfUse || input.otherUse))
            {
                var receiver = input.selfUse ? this : aimedPlayer; //이번 산소팩 사용 대상
                if (itemComponent.tryUse(receiver))
                {
                    presentationComponent.showUse(receiver != this);
                    notify(receiver == this ? "Oxygen replenished" : "Oxygen shared with " + receiver.displayName);
                }
                else notify("No oxygen used - aim closer or check the tank");
            }
            if (input.interact && aimedInteractable != null)
            {
                if (aimedInteractable.interact(this))
                {
                    presentationComponent.playFeedback();
                    notify("Supply received");
                }
                else notify("Tank or inventory is already full");
            }
        }

        private void updateAim() //벽에 가리지 않은 가장 가까운 상호작용 대상 확인
        {
            aimedPlayer = null;
            aimedInteractable = null;
            if (viewCamera == null) return;
            if (!Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit,
                interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return; //조준 충돌 결과
            aimedPlayer = hit.collider.GetComponentInParent<Player>();
            if (aimedPlayer == this) aimedPlayer = null;
            aimedInteractable = hit.collider.GetComponentInParent<DiveInteractableComponent>();
        }

        public void simulateVitals(float deltaTime) //공간 판정 이후 산소와 손목 표현 갱신
        {
            if (paused) return;
            oxygenComponent.simulate(deltaTime, isInAirPocket, swimmingFast);
            if (watchComponent != null) watchComponent.present(depth, oxygenPercent);
            if (presentationComponent != null)
            {
                presentationComponent.present(deltaTime, movementComponent != null ? movementComponent.getSpeedRatio() : 0f,
                    watchComponent != null && watchComponent.isVisible, hasEquippedItem);
                if (oxygenPercent <= 20f && !oxygenWarningPlayed)
                {
                    oxygenWarningPlayed = true;
                    presentationComponent.playFeedback();
                }
                if (oxygenPercent > 20f) oxygenWarningPlayed = false;
            }
        }

        public void setAirPocket(AirPocketComponent pocket) //세션에서 판정한 현재 공간 적용
        {
            currentPocket = pocket;
        }

        public void configureNetworkControl(bool isOwner, string playerName) //네트워크 권한에 따라 로컬 조작과 시점 설정
        {
            locallyControlled = isOwner;
            diverName = playerName;
            if (viewCamera != null) viewCamera.gameObject.SetActive(isOwner);
            var controller = GetComponent<CharacterController>(); //로컬 충돌 계산 대상
            if (controller != null) controller.enabled = isOwner;
            setPaused(isOwner);
        }

        public void simulateNetworkMovement(PlayerInputComponent.Frame input, float deltaTime) //이동 동기화 증분에 필요한 로컬 기능만 호출
        {
            if (!locallyControlled || paused) return;
            movementComponent.simulate(input, deltaTime, true);
            watchComponent.setVisible(input.watch);
            watchComponent.present(depth, oxygenPercent);
            presentationComponent.present(deltaTime, movementComponent.getSpeedRatio(), watchComponent.isVisible, false);
        }

        public bool receiveOxygen(float amount) //아이템과 장치의 산소 공급 진입점
        {
            return oxygenComponent.restoreOxygen(amount);
        }

        public bool receivePack() //보급품 획득 진입점
        {
            return itemComponent != null && itemComponent.addPack();
        }

        public void setPaused(bool shouldPause) //시제품 입력과 커서 상태 전환
        {
            paused = shouldPause;
            if (movementComponent != null) movementComponent.stopMovement();
            if (watchComponent != null && shouldPause) watchComponent.setVisible(false);
            if (!locallyControlled) return;
            Cursor.lockState = shouldPause ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = shouldPause;
        }

        public string getInteractionPrompt() //현재 조준 대상에 맞는 안내 생성
        {
            if (aimedInteractable != null) return "[E]  " + aimedInteractable.label;
            if (aimedPlayer != null) return $"{aimedPlayer.displayName}  /  O2 {aimedPlayer.oxygenPercent:0}%\n"
                + (hasEquippedItem ? "[RMB]  SHARE OXYGEN" : "[Q]  EQUIP OXYGEN PACK");
            return hasEquippedItem ? "[LMB]  USE ON SELF     [RMB]  USE ON DIVER" : "[HOLD RMB]  CHECK WATCH";
        }

        private void notify(string text) //짧은 행동 결과 안내 설정
        {
            notification = text;
            notificationTime = 3f;
        }

        private void OnApplicationFocus(bool hasFocus) //다른 창으로 이동할 때 조작 중단
        {
            if (!hasFocus && locallyControlled) setPaused(true);
        }

        private void OnDisable() //씬 종료 시 마우스 잠금 해제
        {
            if (!locallyControlled) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
