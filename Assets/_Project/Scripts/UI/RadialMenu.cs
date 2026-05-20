using System.Collections.Generic;
using UnityEngine;

namespace KRTD.UI
{
    /// <summary>
    /// 한 위치(보통 클릭한 BuildSpot 의 중심)에 자식 RadialMenuItem 들을
    /// 원형으로 펼치는 컨테이너.
    ///
    /// 사용 예:
    ///   var menu = Instantiate(menuPrefab);
    ///   menu.transform.position = spot.CenterWorld;
    ///   menu.Open(items);   // items: (icon, callback) 묶음
    /// </summary>
    public class RadialMenu : MonoBehaviour
    {
        public struct Entry
        {
            public Sprite icon;
            public System.Action onClick;
            /// <summary>
            /// 이 항목을 고정 각도에 배치하고 싶을 때 사용. null 이면 startAngleDeg + step*i 로 자동 배치.
            /// 0 = 12시, 90 = 3시, 180 = 6시, 270 = 9시.
            /// </summary>
            public float? overrideAngleDeg;

            public Entry(Sprite icon, System.Action onClick, float? overrideAngleDeg = null)
            {
                this.icon = icon;
                this.onClick = onClick;
                this.overrideAngleDeg = overrideAngleDeg;
            }
        }

        [Header("아이템 프리팹")]
        [Tooltip("RadialMenuItem 컴포넌트가 붙은 프리팹. 동적으로 N개 생성된다.")]
        [SerializeField] private RadialMenuItem itemPrefab;

        [Header("배치")]
        [Tooltip("아이템들이 놓일 원의 반지름 (월드 단위).")]
        [SerializeField] private float radius = 1.8f;

        [Tooltip("12시 방향에서 시계 방향으로 첫 아이템이 놓이는 각도 오프셋(도).")]
        [SerializeField] private float startAngleDeg = 0f;

        [Tooltip("아이템들이 차지하는 전체 각도. 360 = 완전한 원, 180 = 위쪽 반원 등.")]
        [SerializeField, Range(30f, 360f)] private float spreadAngleDeg = 360f;

        [Header("등장 애니메이션")]
        [Tooltip("중심에서 최종 위치까지 펼쳐지는 시간(초).")]
        [SerializeField] private float openDuration = 0.18f;

        [Tooltip("닫힐 때 중심으로 수렴하는 시간(초).")]
        [SerializeField] private float closeDuration = 0.10f;

        private readonly List<RadialMenuItem> spawnedItems = new List<RadialMenuItem>();
        private readonly List<Vector3> targetLocalPositions = new List<Vector3>();
        private float animTime;
        private bool opening;
        private bool closing;
        private System.Action onClosed;

        /// <summary>
        /// 메뉴를 열고 아이템들을 원형으로 펼친다.
        /// </summary>
        public void Open(IReadOnlyList<Entry> entries, System.Action onClosedCallback = null)
        {
            ClearItems();
            onClosed = onClosedCallback;

            if (itemPrefab == null || entries == null || entries.Count == 0)
            {
                Debug.LogWarning("[RadialMenu] itemPrefab 이 비었거나 entries 가 없다.");
                return;
            }

            int count = entries.Count;
            float step = (Mathf.Approximately(spreadAngleDeg, 360f) || count <= 1)
                ? 360f / Mathf.Max(count, 1)
                : spreadAngleDeg / (count - 1);

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                var item = Instantiate(itemPrefab, transform);
                item.transform.localPosition = Vector3.zero;

                // 닫힌 뒤 콜백이 살아있도록 캡처
                var captured = entry.onClick;
                item.Setup(entry.icon, () =>
                {
                    captured?.Invoke();
                    Close();
                });

                spawnedItems.Add(item);

                // Unity 좌표: 0도 = 12시(+Y) 기준으로, 시계 방향으로 증가
                float angleDeg = entry.overrideAngleDeg ?? (startAngleDeg + step * i);
                float angleRad = (90f - angleDeg) * Mathf.Deg2Rad;
                targetLocalPositions.Add(new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    Mathf.Sin(angleRad) * radius,
                    0f));
            }

            animTime = 0f;
            opening = true;
            closing = false;
        }

        /// <summary>
        /// 메뉴를 중심으로 수렴시킨 뒤 자기 자신을 파괴한다.
        /// </summary>
        public void Close()
        {
            if (closing) return;
            opening = false;
            closing = true;
            animTime = 0f;
        }

        private void Update()
        {
            if (opening)
            {
                animTime += Time.unscaledDeltaTime;
                float t = openDuration <= 0f ? 1f : Mathf.Clamp01(animTime / openDuration);
                float eased = EaseOutBack(t);
                ApplyPositions(eased);
                if (t >= 1f) opening = false;
            }
            else if (closing)
            {
                animTime += Time.unscaledDeltaTime;
                float t = closeDuration <= 0f ? 1f : Mathf.Clamp01(animTime / closeDuration);
                ApplyPositions(1f - t);
                if (t >= 1f)
                {
                    closing = false;
                    onClosed?.Invoke();
                    Destroy(gameObject);
                }
            }
        }

        private void ApplyPositions(float t)
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] == null) continue;
                spawnedItems[i].transform.localPosition = Vector3.Lerp(Vector3.zero, targetLocalPositions[i], t);
                spawnedItems[i].transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, t);
                spawnedItems[i].SetInteractable(t > 0.9f);
            }
        }

        private void ClearItems()
        {
            foreach (var it in spawnedItems)
            {
                if (it != null) Destroy(it.gameObject);
            }
            spawnedItems.Clear();
            targetLocalPositions.Clear();
        }

        // s 가 커질수록 더 튀어오르는 느낌. 1.70158 은 표준 EaseOutBack 계수.
        private static float EaseOutBack(float t)
        {
            const float s = 1.70158f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        /// <summary>
        /// 라디얼 메뉴의 아이템 콜라이더 중 하나라도 포함하는지.
        /// 외부 클릭으로 닫기 처리할 때 사용.
        /// </summary>
        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            foreach (var item in spawnedItems)
            {
                if (item == null) continue;
                var col = item.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(worldPoint)) return true;
            }
            return false;
        }
    }
}
