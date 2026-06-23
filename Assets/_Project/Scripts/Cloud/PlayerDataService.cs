using System;
using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// 게임 전용 플레이어 데이터 저장소. users/{uid} 경로 규칙과 PlayerData 를 알고,
    /// 범용 CloudSaveManager + AuthManager 를 조합해 읽고 쓴다.
    /// (다른 프로젝트에선 모델 + 이 서비스만 새로 작성하면 됨)
    /// </summary>
    public class PlayerDataService : MonoBehaviour
    {
        public static PlayerDataService Instance { get; private set; }

        private const string UsersNode = "users";

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

        public bool CanUse =>
            CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsReady &&
            AuthManager.Instance != null && AuthManager.Instance.IsSignedIn;

        private string MyPath => $"{UsersNode}/{AuthManager.Instance.UserId}";

        /// <summary>현재 계정에 PlayerData 전체 저장. nowUnixMs 는 호출부에서 주입.</summary>
        public void Save(PlayerData data, long nowUnixMs, Action<bool> onComplete = null)
        {
            if (!CanUse || data == null)
            {
                onComplete?.Invoke(false);
                return;
            }
            if (data.createdAtUnixMs == 0) data.createdAtUnixMs = nowUnixMs;
            data.updatedAtUnixMs = nowUnixMs;
            CloudSaveManager.Instance.Save(MyPath, data, onComplete);
        }

        /// <summary>현재 계정 데이터 로드. 신규 계정이면 null.</summary>
        public void Load(Action<PlayerData> onLoaded)
        {
            if (!CanUse) { onLoaded?.Invoke(null); return; }
            CloudSaveManager.Instance.Load(MyPath, onLoaded);
        }

        /// <summary>볼륨만 부분 업데이트(다른 필드 보존).</summary>
        public void SaveVolumes(float bgm, float sfx, long nowUnixMs, Action<bool> onComplete = null)
        {
            if (!CanUse) { onComplete?.Invoke(false); return; }
            var updates = new Dictionary<string, object>
            {
                ["bgmVolume"] = Mathf.Clamp01(bgm),
                ["sfxVolume"] = Mathf.Clamp01(sfx),
                ["updatedAtUnixMs"] = nowUnixMs,
            };
            CloudSaveManager.Instance.UpdateFields(MyPath, updates, onComplete);
        }
    }
}
