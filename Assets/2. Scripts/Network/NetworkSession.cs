using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoDeep
{
    [RequireComponent(typeof(FusionConnectionComponent))]
    public sealed class NetworkSession : MonoBehaviour
    {
        public enum ConnectionState { Idle, Connecting, Connected, Leaving }
        [SerializeField] private FusionConnectionComponent connectionComponent; //Fusion 연결 구성 요소
        private ConnectionState connectionState; //현재 연결 단계
        private string statusMessage = "Create a room or enter an 8-character code."; //메뉴 연결 안내
        private bool quitting; //종료 중 씬 이동 방지 여부

        public static NetworkSession instance { get; private set; } //씬 사이에 유지할 네트워크 진입점
        public ConnectionState state => connectionState; //UI에 전달할 연결 단계
        public bool onlineRequested => connectionState != ConnectionState.Idle; //온라인 씬 구성 여부
        public bool busy => connectionState == ConnectionState.Connecting || connectionState == ConnectionState.Leaving; //중복 요청 차단 상태
        public string status => statusMessage; //현재 연결 안내
        public string roomCode => connectionComponent.roomCode; //친구에게 전달할 방 코드
        public string region => connectionComponent.region; //같은 방에 사용할 서버 지역
        public int playerCount => connectionComponent.playerCount; //현재 방 인원
        public Player localPlayer => connectionComponent.localPlayer; //씬 UI에 연결할 자기 다이버

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void resetInstance() //도메인 재로드 없는 플레이 모드에서도 정적 참조 초기화
        {
            instance = null;
        }

        private void Awake() //씬 사이에 유지할 단일 진입점 구성
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            if (connectionComponent == null) connectionComponent = GetComponent<FusionConnectionComponent>();
            connectionComponent.initialize(this);
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }

        public Task<bool> createRoom() //새 코드로 최대 4인 방 생성
        {
            return connect(Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(), true);
        }

        public Task<bool> joinRoom(string code) //사용자가 입력한 기존 방으로만 입장
        {
            return connect((code ?? "").Trim().ToUpperInvariant(), false);
        }

        public static bool isValidRoomCode(string code) //공백과 임의 세션 이름을 연결 전에 차단
        {
            if (string.IsNullOrEmpty(code) || code.Length != 8) return false;
            foreach (var character in code) //검사할 코드 문자
                if (!(character >= 'A' && character <= 'Z') && !(character >= '0' && character <= '9')) return false;
            return true;
        }

        private async Task<bool> connect(string code, bool create) //상태 전환과 연결 실패 복구 조율
        {
            if (connectionState != ConnectionState.Idle) return false;
            if (!isValidRoomCode(code))
            {
                statusMessage = "Enter exactly 8 letters or numbers.";
                return false;
            }
            connectionState = ConnectionState.Connecting;
            statusMessage = create ? "Creating room..." : "Joining room...";
            var error = await connectionComponent.connect(code, create); //연결 실패 시 표시할 안전한 안내
            if (this == null || quitting) return false;
            if (error != null)
            {
                connectionState = ConnectionState.Idle;
                statusMessage = error;
                if (SceneManager.GetActiveScene().name == "Play") await SceneManager.LoadSceneAsync("StandBy");
                return false;
            }
            connectionState = ConnectionState.Connected;
            statusMessage = "Connected. Share the room code with your friends.";
            return true;
        }

        public async Task leaveRoom(string message = "Left the room.") //연결을 정리한 뒤 준비 화면 복귀
        {
            if (connectionState == ConnectionState.Leaving || connectionState == ConnectionState.Connecting) return;
            connectionState = ConnectionState.Leaving;
            statusMessage = "Leaving room...";
            await connectionComponent.disconnect();
            if (this == null || quitting) return;
            connectionState = ConnectionState.Idle;
            statusMessage = message;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (SceneManager.GetActiveScene().name != "StandBy") await SceneManager.LoadSceneAsync("StandBy");
        }

        public async void handleDisconnect(string reason) //진행 중 연결 종료를 사용자 안내와 메뉴 복귀로 처리
        {
            if (connectionState == ConnectionState.Connected && !quitting)
                await leaveRoom("Connection ended: " + reason + ". You can try again.");
        }

        private void OnApplicationQuit() //종료 중 비동기 복귀 차단
        {
            quitting = true;
        }

        private void OnDestroy() //파괴한 진입점의 정적 참조 해제
        {
            if (instance == this) instance = null;
        }
    }
}
