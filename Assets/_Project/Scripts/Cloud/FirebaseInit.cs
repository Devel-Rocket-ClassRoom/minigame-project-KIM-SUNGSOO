using System;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// Firebase 의존성 점검·초기화를 단 한 번 수행하는 부트스트랩 싱글톤.
    /// <see cref="DontDestroyOnLoad"/> 로 씬 전환에도 살아남는다.
    ///
    /// Firebase 의 어떤 기능(Auth, Database)이든 사용하기 전에 반드시
    /// <see cref="FirebaseApp.CheckAndFixDependenciesAsync"/> 가 끝나야 한다.
    /// 따라서 AuthManager / CloudSaveManager 는 이 클래스의 <see cref="OnReady"/> 를
    /// 기다렸다가(또는 이미 준비됐으면 즉시) 자기 초기화를 한다.
    ///
    /// 사용:
    ///   1) 첫 진입 씬(MainMenu)에 빈 GameObject 만들고 이 컴포넌트 부착.
    ///   2) 같은 오브젝트(또는 별도)에 AuthManager / CloudSaveManager 부착.
    /// </summary>
    [DefaultExecutionOrder(-200)] // AuthManager(-100 류)보다 먼저 깨도록.
    public class FirebaseInit : MonoBehaviour
    {
        public static FirebaseInit Instance { get; private set; }

        /// <summary>의존성 해결까지 끝나 Firebase 사용이 가능한 상태.</summary>
        public static bool IsReady { get; private set; }

        /// <summary>초기화가 완료된 순간 한 번 발사. 이미 준비된 뒤 구독하면 호출되지 않으니
        /// 구독 전에 <see cref="IsReady"/> 를 먼저 확인할 것.</summary>
        public static event Action OnReady;

        public FirebaseApp App { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatic()
        {
            Instance = null;
            IsReady = false;
            OnReady = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitializeAsync()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[Firebase] 의존성 점검 실패: {task.Exception}");
                    return;
                }

                DependencyStatus status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    App = FirebaseApp.DefaultInstance;
                    IsReady = true;
                    Debug.Log("[Firebase] 초기화 완료 — Auth/Database 사용 가능.");
                    OnReady?.Invoke();
                }
                else
                {
                    // 안드로이드에서 Google Play 서비스 미설치/구버전 등이 여기로 온다.
                    Debug.LogError($"[Firebase] 의존성 해결 불가: {status}");
                }
            });
        }
    }
}
