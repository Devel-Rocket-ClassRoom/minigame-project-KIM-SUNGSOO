using System;
using System.Collections.Generic;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// 범용 RTDB 접근 서비스. 임의 경로에 임의 직렬화 타입(JsonUtility)을 읽고 쓴다.
    /// 특정 게임 데이터에 의존하지 않아 다른 프로젝트에 그대로 이식 가능.
    /// 경로 규칙·게임 모델은 PlayerDataService 같은 게임 전용 계층에서 다룬다.
    /// </summary>
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager Instance { get; private set; }

        private DatabaseReference root;

        // 자동 로그인 시 호출이 Setup 보다 빠를 수 있어 구독 대신 지연 초기화한다.
        private DatabaseReference Root
        {
            get
            {
                if (root == null && FirebaseInit.IsReady)
                    root = FirebaseDatabase.DefaultInstance.RootReference;
                return root;
            }
        }

        /// <summary>Firebase 준비 완료 여부 (로그인 여부는 호출부가 판단).</summary>
        public bool IsReady => Root != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatic() { Instance = null; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>지정 경로에 객체 전체를 JSON 으로 저장(덮어쓰기).</summary>
        public void Save<T>(string path, T data, Action<bool> onComplete = null)
        {
            if (!IsReady || data == null)
            {
                Debug.LogWarning($"[CloudSave] 저장 불가({path}) — Firebase 준비 상태를 확인하세요.");
                onComplete?.Invoke(false);
                return;
            }

            Root.Child(path).SetRawJsonValueAsync(JsonUtility.ToJson(data))
                .ContinueWithOnMainThread(t =>
                {
                    bool ok = !t.IsFaulted && !t.IsCanceled;
                    if (!ok) Debug.LogError($"[CloudSave] 저장 실패({path}): {t.Exception}");
                    onComplete?.Invoke(ok);
                });
        }

        /// <summary>지정 경로를 읽어 T 로 역직렬화. 노드가 없거나 실패하면 null.</summary>
        public void Load<T>(string path, Action<T> onLoaded) where T : class
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[CloudSave] 로드 불가({path}) — Firebase 준비 상태를 확인하세요.");
                onLoaded?.Invoke(null);
                return;
            }

            Root.Child(path).GetValueAsync()
                .ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted || t.IsCanceled)
                    {
                        Debug.LogError($"[CloudSave] 로드 실패({path}): {t.Exception}");
                        onLoaded?.Invoke(null);
                        return;
                    }

                    DataSnapshot snapshot = t.Result;
                    if (snapshot == null || !snapshot.Exists)
                    {
                        onLoaded?.Invoke(null);
                        return;
                    }

                    try
                    {
                        onLoaded?.Invoke(JsonUtility.FromJson<T>(snapshot.GetRawJsonValue()));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CloudSave] 파싱 실패({path}): {ex}");
                        onLoaded?.Invoke(null);
                    }
                });
        }

        /// <summary>지정 경로의 일부 필드만 부분 업데이트(나머지 보존).</summary>
        public void UpdateFields(string path, Dictionary<string, object> updates, Action<bool> onComplete = null)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[CloudSave] 부분저장 불가({path}) — Firebase 준비 상태를 확인하세요.");
                onComplete?.Invoke(false);
                return;
            }

            Root.Child(path).UpdateChildrenAsync(updates)
                .ContinueWithOnMainThread(t =>
                {
                    bool ok = !t.IsFaulted && !t.IsCanceled;
                    if (!ok) Debug.LogError($"[CloudSave] 부분저장 실패({path}): {t.Exception}");
                    onComplete?.Invoke(ok);
                });
        }
    }
}
