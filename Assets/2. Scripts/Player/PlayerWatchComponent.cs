using UnityEngine;
using UnityEngine.UI;

namespace GoDeep
{
    public sealed class PlayerWatchComponent : MonoBehaviour
    {
        [SerializeField] private Text wristReadout; //손목 화면의 깊이와 산소 표시
        [SerializeField] private CanvasGroup detailsPanel; //워치를 볼 때 나타나는 신호 안내
        [SerializeField] private Text detailsReadout; //워치 상세 정보와 최근 신호
        private bool visible; //워치 확인 상태
        private string latestSignal = "No signal selected"; //로컬 신호 미리보기
        private static readonly string[] signals = { "FOLLOW ME", "WAIT HERE", "NEED HELP", "LOW OXYGEN" }; //초기 워치 신호

        public bool isVisible => visible; //팔 표현에 전달할 워치 상태
        public string lastSignal => latestSignal; //테스트에서 확인할 마지막 신호

        public void setVisible(bool shouldShow) //워치 보기 상태 전환
        {
            visible = shouldShow;
            if (detailsPanel != null) detailsPanel.alpha = visible ? 1f : 0f;
        }

        public bool selectSignal(int signal) //워치를 보고 있을 때만 로컬 신호 선택
        {
            if (!visible || signal < 1 || signal > signals.Length) return false;
            latestSignal = "LOCAL  /  " + signals[signal - 1];
            return true;
        }

        public void present(float depth, float oxygen) //상태 수치를 손목 화면과 상세 안내에 표시
        {
            if (wristReadout != null) wristReadout.text = $"O2  {oxygen:000}%\n{depth:00.0} m";
            if (detailsReadout != null)
                detailsReadout.text = $"{depth:0.0} m    /    O2 {oxygen:0}%\n\n1  FOLLOW ME      2  WAIT HERE\n3  NEED HELP      4  LOW OXYGEN\n\n{latestSignal}";
        }
    }
}
