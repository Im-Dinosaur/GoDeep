using UnityEngine;
using UnityEngine.UI;

namespace GoDeep
{
    public sealed class NetworkMenuComponent : MonoBehaviour
    {
        [SerializeField] private InputField roomCodeInput; //친구의 방 코드 입력란
        [SerializeField] private Text statusText; //연결과 실패 안내
        [SerializeField] private Button createButton; //방 생성 버튼
        [SerializeField] private Button joinButton; //기존 방 입장 버튼
        [SerializeField] private Button localButton; //로컬 연습 버튼
        [SerializeField] private Button backButton; //홈 복귀 버튼

        private void Update() //진입점의 현재 연결 상태를 메뉴에 표시
        {
            var session = NetworkSession.instance; //씬을 넘어 유지되는 연결 진입점
            var available = session != null && session.state == NetworkSession.ConnectionState.Idle; //새 요청 허용 여부
            createButton.interactable = available;
            joinButton.interactable = available && NetworkSession.isValidRoomCode(roomCodeInput.text.Trim().ToUpperInvariant());
            localButton.interactable = available;
            backButton.interactable = available;
            roomCodeInput.interactable = available;
            statusText.text = session != null ? session.status + "\nREGION: " + session.region.ToUpperInvariant() + "  /  MAX 4 DIVERS" : "Preparing connection...";
        }

        public async void createRoom() //버튼 입력을 방 생성 진입점에 전달
        {
            if (NetworkSession.instance != null) await NetworkSession.instance.createRoom();
        }

        public async void joinRoom() //버튼 입력을 기존 방 입장 진입점에 전달
        {
            if (NetworkSession.instance != null) await NetworkSession.instance.joinRoom(roomCodeInput.text);
        }
    }
}
