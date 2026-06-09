using UnityEngine;

namespace KRTD.Game
{
    /// <summary>
    /// 앱 시작 시 한 번 실행되는 전역 초기화. 씬 GameObject 가 필요 없어
    /// <see cref="RuntimeInitializeOnLoadMethod"/> 로 자동 호출된다.
    ///
    /// 주 목적: 모바일(Android)에서 Unity 가 <c>Application.targetFrameRate</c> 를
    /// 기본 30 으로 캡 하는 동작을 해제. 30fps 에서는 화살 같은 투사체가
    /// 프레임당 이동량이 hitRadius 보다 커져 적을 건너뛰고 진동하는
    /// "잔존" 현상이 발생한다 (Arrow/Magic 의 step-aware 명중 판정과 함께 보완).
    /// </summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // vSync 와 targetFrameRate 는 동시 사용 시 vSync 가 우선하므로 끄고
            // targetFrameRate 로 명시. 모바일 배터리 영향은 있으나 게임 반응성/투사체
            // 명중 일관성을 우선한다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
    }
}
