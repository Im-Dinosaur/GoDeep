using UnityEngine;

namespace GoDeep
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementComponent : MonoBehaviour
    {
        [SerializeField] private CharacterController controller; //벽 충돌을 처리할 컨트롤러
        [SerializeField] private Transform viewPivot; //상하 시점 회전 기준
        [SerializeField, Min(0.1f)] private float swimSpeed = 3.2f; //기본 수영 속도
        [SerializeField, Min(0.1f)] private float fastSpeed = 4.8f; //빠른 수영 속도
        [SerializeField, Min(0.1f)] private float acceleration = 7f; //수영 가속도
        [SerializeField, Min(0.1f)] private float deceleration = 5f; //수영 감속도
        [SerializeField, Range(0.01f, 1f)] private float lookSensitivity = 0.1f; //마우스 감도
        private Vector3 velocity; //현재 수영 속도 벡터
        private float pitch; //시점의 상하 각도

        private void Awake() //필수 이동 참조 초기화
        {
            if (controller == null) controller = GetComponent<CharacterController>();
        }

        public void simulate(PlayerInputComponent.Frame input, float deltaTime, bool canMove) //시점과 충돌 수영 갱신
        {
            if (viewPivot == null || controller == null || !controller.enabled) return;
            transform.Rotate(0f, input.look.x * lookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - input.look.y * lookSensitivity, -80f, 80f);
            viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (!canMove)
            {
                velocity = Vector3.zero;
                return;
            }
            var direction = Vector3.ClampMagnitude(viewPivot.forward * input.movement.y + transform.right * input.movement.x
                + Vector3.up * input.vertical, 1f); //대각선 입력을 보정한 이동 방향
            var targetVelocity = direction * (input.fast ? fastSpeed : swimSpeed); //목표 수영 속도
            velocity = Vector3.MoveTowards(velocity, targetVelocity,
                (direction.sqrMagnitude > 0.001f ? acceleration : deceleration) * deltaTime);
            controller.Move(velocity * deltaTime);
        }

        public float getSpeedRatio() //팔 동작에 전달할 정규화 이동 속도
        {
            return Mathf.Clamp01(velocity.magnitude / fastSpeed);
        }

        public void stopMovement() //일시정지와 종료 시 잔여 속도 제거
        {
            velocity = Vector3.zero;
        }
    }
}
