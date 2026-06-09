using UnityEngine;
using UnityEngine.UI;

namespace KRTD.Audio
{
    /// <summary>
    /// Button 옆에 붙이기만 하면 클릭 시 <see cref="AudioManager.PlayButtonClick"/> 가 자동 호출된다.
    /// 추가 코드/씬 와이어링 없이 인스펙터 드롭만으로 클릭 사운드 연결.
    ///
    /// 사용:
    ///   - 버튼 프리팹(또는 인스턴스) 에 Add Component → KRTD/Audio/Button Click Sfx
    ///   - 클릭음 자체는 AudioManager 의 buttonClickSfx 필드에서 한 곳에서 관리 (이 컴포넌트는 단순 트리거)
    ///
    /// 인터랙터블이 꺼진 상태에서는 onClick 이 발화하지 않으므로 자동으로 무음 — 별도 분기 불필요.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("KRTD/Audio/Button Click Sfx")]
    public class ButtonClickSfx : MonoBehaviour
    {
        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            var am = AudioManager.Instance;
            if (am != null) am.PlayButtonClick();
        }
    }
}
