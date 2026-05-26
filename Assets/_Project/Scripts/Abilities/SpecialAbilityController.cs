using UnityEngine;
using UnityEngine.EventSystems;

namespace KRTD.Abilities
{
    /// <summary>
    /// 씬에 하나만 존재하는 특수능력 입력/조준 컨트롤러.
    /// UI 버튼이 BeginTargeting 을 호출하면 "조준 모드" 로 들어가고,
    /// 다음 월드 클릭에서 능력을 시전한다. 우클릭/ESC 로 취소.
    ///
    /// UI 위 클릭은 무시 (EventSystem.IsPointerOverGameObject 로 판정).
    /// </summary>
    public class SpecialAbilityController : MonoBehaviour
    {
        public static SpecialAbilityController Instance { get; private set; }

        [Header("선택 사항")]
        [Tooltip("월드 클릭 좌표를 얻을 카메라. 비우면 Camera.main 사용.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("조준 모드 시 마우스 위치에 미리보기 원을 그릴 LineRenderer 프리팹(원형 1회 둘러그림). " +
            "null 이면 미리보기 표시 없음.")]
        [SerializeField] private LineRenderer previewCirclePrefab;

        private SpecialAbility pendingAbility;
        private LineRenderer previewInstance;

        public bool IsTargeting => pendingAbility != null;
        public SpecialAbility PendingAbility => pendingAbility;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>UI 버튼이 호출. 쿨다운 중이면 무시.</summary>
        public void BeginTargeting(SpecialAbility ability)
        {
            if (ability == null || !ability.IsReady) return;

            // 이미 다른 능력을 조준 중이었으면 취소하고 갈아끼움.
            CancelTargeting();

            pendingAbility = ability;
            ShowPreview(ability);
        }

        public void CancelTargeting()
        {
            pendingAbility = null;
            HidePreview();
        }

        private void Update()
        {
            if (!IsTargeting) return;

            // 카메라가 씬 전환 등으로 null 이 됐을 수 있으니 보강.
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null) return;
            }

            // 미리보기 원을 마우스 따라가게.
            if (previewInstance != null)
            {
                Vector3 mouseWorld = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                previewInstance.transform.position = mouseWorld;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelTargeting();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                // UI 위 클릭(능력 버튼 자체 등) 은 무시.
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                Vector3 mouseWorld = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;

                var ability = pendingAbility;
                // 캐스트 자체에서 실패해도 조준 모드는 닫는다 (쿨 끝났는지 다시 누르라는 의미).
                CancelTargeting();
                ability.TryCast(mouseWorld);
            }
        }

        private void ShowPreview(SpecialAbility ability)
        {
            if (previewCirclePrefab == null) return;

            float radius = ability is IAbilityPreviewRadius p ? p.PreviewRadius : 0f;
            if (radius <= 0f) return;

            previewInstance = Instantiate(previewCirclePrefab);
            DrawCircle(previewInstance, radius, 48);
        }

        private void HidePreview()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance.gameObject);
                previewInstance = null;
            }
        }

        private static void DrawCircle(LineRenderer lr, float radius, int segments)
        {
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2f * Mathf.PI / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }
    }

    /// <summary>
    /// 능력이 조준 모드일 때 보여줄 미리보기 반경을 제공. 구현 안 해도 OK (미리보기 생략).
    /// </summary>
    public interface IAbilityPreviewRadius
    {
        float PreviewRadius { get; }
    }
}
