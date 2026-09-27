using UnityEngine;
using UnityEngine.UI;

namespace GoDeep
{
    public sealed class PrototypeUIComponent : MonoBehaviour
    {
        [SerializeField] private PrototypeSession session; //표시할 시제품 세션
        [SerializeField] private Text locationText; //현재 공간 안내
        [SerializeField] private Text interactionText; //조준 대상 행동 안내
        [SerializeField] private Text inventoryText; //손 상태와 소모품 수
        [SerializeField] private Text notificationText; //짧은 행동 결과
        [SerializeField] private Text warningText; //산소 부족과 행동불가 경고
        [SerializeField] private Text elapsedText; //탐사 시간
        [SerializeField] private CanvasGroup pausePanel; //일시정지와 결과 화면
        [SerializeField] private Text pauseTitle; //현재 일시정지 또는 결과 제목
        [SerializeField] private Text pauseDescription; //현재 안내 내용
        [SerializeField] private Button resumeButton; //시작 또는 이어하기 버튼
        [SerializeField] private Image progressFill; //목표 방향 진행 안내
        [SerializeField, Min(1f)] private float progressWidth = 360f; //진행 표시의 전체 너비

        private void Update() //파사드에서 받은 현재 플레이 상태 표시
        {
            if (session == null || session.player == null) return;
            var player = session.player; //현재 조작 플레이어
            locationText.text = player.isInAirPocket ? player.airPocket.displayName + "  /  AIR" : "FLOODED CAVE  /  WATCH COMMS";
            interactionText.text = player.isPaused ? "" : player.getInteractionPrompt();
            inventoryText.text = $"OXYGEN PACKS   {player.packCount} / 3\n[Q]  " + (player.hasEquippedItem ? "STOW PACK" : "EQUIP PACK");
            notificationText.text = player.message;
            elapsedText.text = $"DIVE  {Mathf.FloorToInt(session.elapsed / 60f):00}:{Mathf.FloorToInt(session.elapsed % 60f):00}";
            warningText.text = player.lifeState == PlayerOxygenComponent.LifeState.Downed
                ? $"OUT OF OXYGEN  /  RESCUE {player.rescueTime:0}s\n[R] RESTART PRACTICE"
                : player.oxygenPercent <= 20f ? "LOW OXYGEN  /  CHECK YOUR WATCH" : "";
            progressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                progressWidth * Mathf.InverseLerp(-9f, 58f, player.transform.position.z));
            var showPanel = player.isPaused || session.state != PrototypeSession.RunState.Exploring; //메뉴 표시 여부
            pausePanel.alpha = showPanel ? 1f : 0f;
            pausePanel.interactable = showPanel;
            pausePanel.blocksRaycasts = showPanel;
            resumeButton.gameObject.SetActive(session.state == PrototypeSession.RunState.Exploring);
            pauseTitle.text = session.state == PrototypeSession.RunState.Complete ? "AIR POCKET REACHED"
                : session.state == PrototypeSession.RunState.Failed ? "DIVE ENDED" : "READY TO GO DEEP?";
            pauseDescription.text = session.state == PrototypeSession.RunState.Complete
                ? "You reached the next pocket.\nThis is the end of the first movement prototype.\nTry the watch, oxygen sharing and supplies on your next dive."
                : session.state == PrototypeSession.RunState.Failed ? "Your oxygen ran out.\nUse a pack or refill before leaving an air pocket."
                : "Reach the warm light at the far end of the cave.\n\nWASD + MOUSE  Swim / look     SPACE / CTRL  Rise / descend\nSHIFT  Swim faster     Q  Equip / stow oxygen pack\nLMB  Use on self     RMB  Use on diver / hold to check watch\nE  Collect / refill     1-4  Watch signal preview\nESC  Pause     R  Restart\n\nLOCAL PRACTICE  /  Online play and voice are not active yet.";
        }
    }
}
