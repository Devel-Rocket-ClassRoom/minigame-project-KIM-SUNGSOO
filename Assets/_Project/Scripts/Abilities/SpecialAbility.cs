using System;
using UnityEngine;

namespace KRTD.Abilities
{
    /// <summary>
    /// 플레이어가 직접 발동하는 특수능력의 공통 베이스.
    /// 쿨다운/조준모드 관리만 담당하고, 실제 효과는 파생 클래스의 <see cref="Perform"/> 가 구현한다.
    ///
    /// 흐름:
    ///   AbilityButton (UI) → SpecialAbilityController.BeginTargeting(this)
    ///   → 플레이어가 월드 한 곳을 클릭
    ///   → SpecialAbilityController.TryCast(worldPos)
    ///   → 이 클래스가 쿨다운 확인 후 Perform(worldPos) 호출 + 쿨다운 시작
    ///
    /// 정책:
    ///   - 비용은 쿨다운만. 골드 차감 없음.
    ///   - 쿨다운 중에는 Begin/TryCast 가 무시된다.
    ///   - 같은 능력의 중복 캐스팅은 막는다 (한 번 시전 후 즉시 쿨다운).
    /// </summary>
    public abstract class SpecialAbility : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("UI 버튼/툴팁에 사용할 이름. 비워두면 GameObject 이름 사용.")]
        [SerializeField] private string displayName;

        [Tooltip("UI 버튼에 표시할 아이콘.")]
        [SerializeField] private Sprite icon;

        [Header("쿨다운")]
        [Tooltip("이 능력의 쿨다운 (초). 시전 직후부터 카운트.")]
        [Min(0.1f)]
        [SerializeField] private float cooldownSeconds = 30f;

        // 시전 시각. -inf 로 초기화해 처음에는 즉시 사용 가능.
        private float lastCastTime = float.NegativeInfinity;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public float CooldownSeconds => cooldownSeconds;

        /// <summary>0 이면 즉시 사용 가능, 양수면 남은 쿨다운 초.</summary>
        public float CooldownRemaining
        {
            get
            {
                float remain = (lastCastTime + cooldownSeconds) - Time.time;
                return remain > 0f ? remain : 0f;
            }
        }

        /// <summary>0~1. UI 의 라디얼 마스크 등에 사용. 1 = 사용 가능, 0 = 막 시전된 상태.</summary>
        public float CooldownProgress01
        {
            get
            {
                if (cooldownSeconds <= 0f) return 1f;
                return Mathf.Clamp01(1f - CooldownRemaining / cooldownSeconds);
            }
        }

        public bool IsReady => CooldownRemaining <= 0f;

        /// <summary>쿨다운 상태가 바뀔 때 발사 (시전 시점과 쿨다운 완료 시점에). UI 가 구독.</summary>
        public event Action OnStateChanged;

        /// <summary>
        /// 컨트롤러가 호출. 준비됐고 위치가 유효하면 효과 실행 후 쿨다운 시작하고 true 반환.
        /// 쿨다운 중이거나 위치 무효면 false (쿨다운 안 들어감 → 다시 시도 가능).
        /// </summary>
        public bool TryCast(Vector3 worldPos)
        {
            if (!IsReady) return false;
            if (!IsValidPlacement(worldPos)) return false;

            Perform(worldPos);
            lastCastTime = Time.time;
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 이 위치에 시전 가능한지 판정. 파생 클래스가 오버라이드 (지원군 = pathtile 위에만 등).
        /// 기본: 어디든 OK (LavaZone 등).
        /// 미리보기 UI 도 이 결과로 가능/불가 색을 결정한다.
        /// </summary>
        public virtual bool IsValidPlacement(Vector3 worldPos) => true;

        /// <summary>파생 클래스가 실제 효과를 구현. 쿨다운 처리는 베이스가 담당하므로 여기선 하지 않는다.</summary>
        protected abstract void Perform(Vector3 worldPos);

        /// <summary>
        /// 현재 쿨다운을 N초만큼 단축한다. 시전 시각(lastCastTime)을 과거로 당기는 방식이라
        /// 이미 시전한 적 없는 능력(IsReady=true)에는 영향이 없다.
        /// 보통 "다음 웨이브 일찍 부르기" 같은 외부 보상 시스템이 호출.
        /// </summary>
        public void ReduceCooldown(float seconds)
        {
            if (seconds <= 0f) return;
            if (IsReady) return; // 이미 준비된 능력은 더 당길 의미 없음
            lastCastTime -= seconds;
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 매 프레임 쿨다운 종료 시점을 한 번만 통지하기 위해 폴링.
        /// (UI 가 매 프레임 CooldownProgress01 을 읽는 방식도 가능하므로 옵션. 여기선 단순화를 위해 이벤트만 사용.)
        /// </summary>
        private bool wasReady = true;
        private void Update()
        {
            bool nowReady = IsReady;
            if (nowReady != wasReady)
            {
                wasReady = nowReady;
                OnStateChanged?.Invoke();
            }
        }
    }
}
