using System;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// Firebase 의존성 점검·초기화를 1회 수행하는 부트스트랩 싱글톤.
    /// Auth/Database 사용 전에 반드시 완료돼야 하므로, AuthManager/CloudSaveManager 는
    /// IsReady 확인 후 OnReady 를 기다린다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class FirebaseInit : MonoBehaviour
    {
        public static FirebaseInit Instance { get; private set; }
        public static bool IsReady { get; private set; }
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

                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogError($"[Firebase] 의존성 해결 불가: {task.Result}");
                    return;
                }

                App = FirebaseApp.DefaultInstance;
                IsReady = true;
                Debug.Log("[Firebase] 초기화 완료.");
                OnReady?.Invoke();
            });
        }
    }
}
