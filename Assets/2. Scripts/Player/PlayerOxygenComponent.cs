using UnityEngine;

namespace GoDeep
{
    public sealed class PlayerOxygenComponent : MonoBehaviour
    {
        public enum LifeState { Active, Downed, Eliminated }
        [SerializeField, Min(1f)] private float maximumOxygen = 100f; //최대 산소량
        [SerializeField, Range(0f, 100f)] private float startingOxygenPercent = 100f; //시작 산소 비율
        [SerializeField, Min(0f)] private float consumptionPerSecond = 0.55f; //초당 산소 소모량
        [SerializeField, Min(1f)] private float fastConsumptionMultiplier = 1.25f; //빠른 수영 소모 배율
        [SerializeField, Min(1f)] private float rescueDuration = 20f; //행동불가 구조 유예 시간
        private float oxygen; //현재 산소량
        private float rescueTime; //남은 구조 시간
        private LifeState lifeState; //현재 생존 상태

        public float oxygenPercent => oxygen / maximumOxygen * 100f; //표시할 산소 비율
        public float remainingRescueTime => rescueTime; //남은 구조 가능 시간
        public LifeState state => lifeState; //현재 생존 상태

        private void Awake() //산소와 생존 상태 초기화
        {
            resetOxygen();
        }

        public void resetOxygen() //시작 설정으로 산소 초기화
        {
            oxygen = maximumOxygen * startingOxygenPercent / 100f;
            rescueTime = rescueDuration;
            lifeState = oxygen > 0f ? LifeState.Active : LifeState.Downed;
        }

        public void simulate(float deltaTime, bool isInAirPocket, bool isSwimmingFast) //산소 소모와 구조 제한 시간 갱신
        {
            if (deltaTime <= 0f || lifeState == LifeState.Eliminated) return;
            if (lifeState == LifeState.Downed)
            {
                rescueTime = Mathf.Max(0f, rescueTime - deltaTime);
                if (rescueTime <= 0f) lifeState = LifeState.Eliminated;
                return;
            }
            if (isInAirPocket) return;
            oxygen = Mathf.Max(0f, oxygen - consumptionPerSecond * (isSwimmingFast ? fastConsumptionMultiplier : 1f) * deltaTime);
            if (oxygen <= 0f)
            {
                lifeState = LifeState.Downed;
                rescueTime = rescueDuration;
            }
        }

        public bool restoreOxygen(float amount) //회복 효과가 있는 경우에만 산소를 보충하고 구조 처리
        {
            if (amount <= 0f || lifeState == LifeState.Eliminated || oxygen >= maximumOxygen) return false;
            oxygen = Mathf.Min(maximumOxygen, oxygen + amount);
            lifeState = LifeState.Active;
            rescueTime = rescueDuration;
            return true;
        }
    }
}
