using UnityEngine;

namespace GoDeep
{
    public sealed class PlayerPresentationComponent : MonoBehaviour
    {
        [SerializeField] private Transform leftArm; //워치를 착용한 팔 기준
        [SerializeField] private Transform rightArm; //산소팩을 사용하는 팔 기준
        [SerializeField] private GameObject heldPack; //손에 든 산소팩 표시
        [SerializeField] private AudioSource feedbackAudio; //워치와 아이템 효과음 출력
        [SerializeField, Range(0f, 1f)] private float armMotionAmount = 0.6f; //수영 팔 동작 강도
        private Vector3 leftRest; //왼팔 기본 위치
        private Vector3 rightRest; //오른팔 기본 위치
        private float animationTime; //수영 동작 시간
        private float useTime; //사용 동작 남은 시간
        private bool otherTarget; //동료 대상 사용 동작 여부
        private AudioClip feedbackClip; //시제품용 짧은 합성 효과음

        private void Awake() //팔 기본 자세와 임시 소리 초기화
        {
            if (leftArm != null) leftRest = leftArm.localPosition;
            if (rightArm != null) rightRest = rightArm.localPosition;
            if (feedbackAudio == null) return;
            const int sampleRate = 22050; //임시 효과음 샘플레이트
            var samples = new float[3308]; //짧은 효과음 샘플
            for (var index = 0; index < samples.Length; index++) //현재 소리 샘플 번호
            {
                var t = (float)index / sampleRate; //샘플 재생 시각
                var envelope = Mathf.Sin(Mathf.PI * index / samples.Length); //클릭 잡음을 줄이는 진폭
                samples[index] = Mathf.Sin(2f * Mathf.PI * (700f * t + 1600f * t * t)) * envelope * 0.16f;
            }
            feedbackClip = AudioClip.Create("PrototypeFeedback", samples.Length, 1, sampleRate, false);
            feedbackClip.SetData(samples, 0);
        }

        public void showUse(bool isOtherTarget) //자기 사용과 동료 사용의 팔 동작 구분
        {
            useTime = 0.55f;
            otherTarget = isOtherTarget;
            playFeedback();
        }

        public void playFeedback() //짧은 시제품 알림음 재생
        {
            if (feedbackAudio != null && feedbackClip != null) feedbackAudio.PlayOneShot(feedbackClip);
        }

        public void present(float deltaTime, float speedRatio, bool watching, bool equipped) //수영과 워치와 아이템 팔 자세 보간
        {
            animationTime += deltaTime * (2f + speedRatio * 3f);
            useTime = Mathf.Max(0f, useTime - deltaTime);
            var bob = Mathf.Sin(animationTime) * 0.035f * speedRatio * armMotionAmount; //수영에 따른 손의 진폭
            var blend = 1f - Mathf.Exp(-14f * deltaTime); //프레임 독립 자세 보간량
            if (leftArm != null)
            {
                var target = watching ? new Vector3(-0.22f, -0.14f, 0.5f) : leftRest + Vector3.up * bob; //왼팔 목표 위치
                leftArm.localPosition = Vector3.Lerp(leftArm.localPosition, target, blend);
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation,
                    Quaternion.Euler(watching ? Vector3.zero : new Vector3(8f, 0f, -8f)), blend);
            }
            if (rightArm != null)
            {
                var use = Mathf.Sin(useTime / 0.55f * Mathf.PI); //사용 동작 진행량
                var offset = otherTarget ? new Vector3(-0.08f, 0.11f, 0.35f) : new Vector3(-0.16f, 0.18f, -0.1f); //대상별 손 이동
                rightArm.localPosition = Vector3.Lerp(rightArm.localPosition, rightRest - Vector3.up * bob + offset * use, blend);
            }
            if (heldPack != null) heldPack.SetActive(equipped || useTime > 0f);
        }

        private void OnDestroy() //실행 중 생성한 임시 소리 정리
        {
            if (feedbackClip != null) Destroy(feedbackClip);
        }
    }
}
