using System;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// 플레이어 데이터를 Firebase Realtime Database 에 저장/로드하는 싱글톤.
    /// 경로 구조: <c>users/{uid}</c> 아래에 <see cref="PlayerData"/> 전체를 JSON 으로 보관.
    ///
    /// 책임:
    ///   - 현재 로그인 사용자(AuthManager.CurrentUser) 기준으로 읽기/쓰기
    ///   - JsonUtility ↔ RTDB(RawJsonValue) 변환
    ///   - 콜백은 메인 스레드(ContinueWithOnMainThread)에서 호출 → Unity API 안전
    ///
    /// DB 루트는 <see cref="Root"/> 프로퍼티로 "지연 초기화" 한다.
    /// AuthManager 가 초기화 직후(OnReady 체인 안에서) 자동 로그인 세션을 감지해
    /// 곧바로 Load 를 호출하는데, 그 시점에 이 매니저의 Setup 이 아직 안 돌았을 수 있다.
    /// 그래서 OnReady 구독에 의존하지 않고, 호출되는 순간 FirebaseInit.IsReady 면 루트를 잡는다.
    ///
    /// 주의:
    ///   - RTDB 보안 규칙에서 본인 uid 노드만 읽기/쓰기 허용하도록 잠가야 한다(가이드 참조).
    /// </summary>
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager Instance { get; private set; }

        private const string UsersNode = "users";

        private DatabaseReference root;

        /// <summary>DB 루트 참조. Firebase 준비가 끝났으면 최초 접근 시 한 번 잡아 캐싱.</summary>
        private DatabaseReference Root
        {
            get
            {
                if (root == null && FirebaseInit.IsReady)
                    root = FirebaseDatabase.DefaultInstance.RootReference;
                return root;
            }
        }

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

        /// <summary>저장/로드가 가능한 상태(Firebase 준비 완료 + 로그인됨)인지.</summary>
        public bool CanUse =>
            Root != null && AuthManager.Instance != null && AuthManager.Instance.IsSignedIn;

        // --- 저장 --------------------------------------------------------------

        /// <summary>
        /// 플레이어 데이터를 현재 계정 노드에 통째로 저장(덮어쓰기).
        /// createdAt 이 비어 있으면 이번 시각으로 채운다. updatedAt 은 항상 갱신.
        /// </summary>
        /// <param name="nowUnixMs">
        /// 저장 시각(Unix epoch ms). 호출부에서
        /// <c>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()</c> 로 넣어준다.
        /// </param>
        /// <param name="onComplete">성공 여부 콜백(선택).</param>
        public void Save(PlayerData data, long nowUnixMs, Action<bool> onComplete = null)
        {
            if (data == null) { onComplete?.Invoke(false); return; }
            if (!CanUse)
            {
                Debug.LogWarning("[CloudSave] 저장 불가 — 초기화/로그인 상태를 확인하세요.");
                onComplete?.Invoke(false);
                return;
            }

            if (data.createdAtUnixMs == 0) data.createdAtUnixMs = nowUnixMs;
            data.updatedAtUnixMs = nowUnixMs;

            string json = JsonUtility.ToJson(data);
            string uid = AuthManager.Instance.UserId;

            Root.Child(UsersNode).Child(uid).SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task =>
                {
                    bool ok = !task.IsFaulted && !task.IsCanceled;
                    if (!ok) Debug.LogError($"[CloudSave] 저장 실패: {task.Exception}");
                    onComplete?.Invoke(ok);
                });
        }

        // --- 로드 --------------------------------------------------------------

        /// <summary>
        /// 현재 계정의 데이터를 로드. 노드가 없으면(신규 계정) null 을 콜백으로 돌려준다 →
        /// 호출부에서 새 <see cref="PlayerData"/> 를 만들어 닉네임 설정 후 Save 하면 된다.
        /// </summary>
        public void Load(Action<PlayerData> onLoaded)
        {
            if (!CanUse)
            {
                Debug.LogWarning("[CloudSave] 로드 불가 — 초기화/로그인 상태를 확인하세요.");
                onLoaded?.Invoke(null);
                return;
            }

            string uid = AuthManager.Instance.UserId;
            Root.Child(UsersNode).Child(uid).GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError($"[CloudSave] 로드 실패: {task.Exception}");
                        onLoaded?.Invoke(null);
                        return;
                    }

                    DataSnapshot snapshot = task.Result;
                    if (snapshot == null || !snapshot.Exists)
                    {
                        onLoaded?.Invoke(null); // 신규 계정.
                        return;
                    }

                    PlayerData data;
                    try
                    {
                        data = JsonUtility.FromJson<PlayerData>(snapshot.GetRawJsonValue());
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CloudSave] JSON 파싱 실패: {ex}");
                        onLoaded?.Invoke(null);
                        return;
                    }
                    onLoaded?.Invoke(data);
                });
        }
    }
}
