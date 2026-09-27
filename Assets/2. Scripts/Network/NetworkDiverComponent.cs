using Fusion;
using UnityEngine;

namespace GoDeep
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Player))]
    public sealed class NetworkDiverComponent : NetworkBehaviour
    {
        [SerializeField] private Player player; //이동과 로컬 표현을 조율하는 파사드
        [SerializeField] private GameObject remoteBody; //다른 참가자에게 보여줄 임시 전신
        [SerializeField] private Transform remoteHead; //상하 시선을 표시할 머리
        private PlayerInputComponent.Frame latestInput; //다음 네트워크 틱에 사용할 지속 입력
        private Vector2 pendingLook; //렌더 프레임 사이에 누적한 마우스 이동
        private bool spawned; //네트워크 상태 접근 가능 여부
        [Networked] public float viewPitch { get; set; } //다른 참가자에게 전달할 상하 시선

        public override void Spawned() //권한에 따라 1인칭과 원격 다이버 구분
        {
            spawned = true;
            if (player == null) player = GetComponent<Player>();
            player.configureNetworkControl(HasStateAuthority, "DIVER " + Object.StateAuthority.RawEncoded);
            if (remoteBody != null) remoteBody.SetActive(!HasStateAuthority);
        }

        private void Update() //화면 프레임의 입력을 잃지 않도록 누적
        {
            if (!spawned || !HasStateAuthority) return;
            latestInput = player.readInput();
            if (latestInput.pause) player.setPaused(!player.isPaused);
            if (player.isPaused)
            {
                latestInput = default;
                pendingLook = Vector2.zero;
                return;
            }
            pendingLook += latestInput.look;
        }

        public override void FixedUpdateNetwork() //자기 상태 권한에서만 충돌 수영 계산
        {
            if (!HasStateAuthority) return;
            latestInput.look = pendingLook;
            pendingLook = Vector2.zero;
            player.simulateNetworkMovement(latestInput, Runner.DeltaTime);
            viewPitch = player.viewPitch;
        }

        public override void Render() //원격 머리에 동기화된 시선 적용
        {
            if (!HasStateAuthority && remoteHead != null) remoteHead.localRotation = Quaternion.Euler(viewPitch, 0f, 0f);
        }

        public override void Despawned(NetworkRunner activeRunner, bool hasState) //제거 후 네트워크 프로퍼티 접근 방지
        {
            spawned = false;
        }
    }
}
