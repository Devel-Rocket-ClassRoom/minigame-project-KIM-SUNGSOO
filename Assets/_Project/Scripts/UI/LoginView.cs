using System;
using Firebase.Auth;
using KRTD.Audio;
using KRTD.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 이메일/비밀번호 로그인 폼. 입력 → 인증 → 데이터 로드(신규면 생성)까지 처리하고
    /// OnLoginComplete 로 결과를 알린다. 패널 전환은 MainMenuView 가 담당.
    /// </summary>
    public class LoginView : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button signUpButton;

        [Header("표시")]
        [SerializeField] private TMP_Text statusText;

        public event Action<PlayerData> OnLoginComplete;

        private bool pendingSignUp;
        private string pendingNickname;

        private void OnEnable()
        {
            if (loginButton != null) loginButton.onClick.AddListener(HandleLogin);
            if (signUpButton != null) signUpButton.onClick.AddListener(HandleSignUp);

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnSignedIn += HandleSignedIn;
                AuthManager.Instance.OnError += HandleError;

                if (AuthManager.Instance.IsSignedIn) SetStatus("자동 로그인 중...");
            }
        }

        private void OnDisable()
        {
            if (loginButton != null) loginButton.onClick.RemoveListener(HandleLogin);
            if (signUpButton != null) signUpButton.onClick.RemoveListener(HandleSignUp);

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnSignedIn -= HandleSignedIn;
                AuthManager.Instance.OnError -= HandleError;
            }
        }

        private void HandleLogin()
        {
            if (AuthManager.Instance == null) { SetStatus("인증 시스템이 준비되지 않았습니다."); return; }
            pendingSignUp = false;
            SetStatus("로그인 중...");
            AuthManager.Instance.SignIn(GetText(emailInput), GetText(passwordInput));
        }

        private void HandleSignUp()
        {
            if (AuthManager.Instance == null) { SetStatus("인증 시스템이 준비되지 않았습니다."); return; }
            pendingNickname = GetText(nicknameInput).Trim();
            if (string.IsNullOrEmpty(pendingNickname))
            {
                SetStatus("닉네임을 입력하세요.");
                return;
            }
            pendingSignUp = true;
            SetStatus("회원가입 중...");
            AuthManager.Instance.SignUp(GetText(emailInput), GetText(passwordInput));
        }

        private void HandleSignedIn(FirebaseUser user)
        {
            SetStatus("데이터 불러오는 중...");

            if (PlayerDataService.Instance == null)
            {
                SetStatus("저장 시스템이 준비되지 않았습니다.");
                return;
            }

            PlayerDataService.Instance.Load(data =>
            {
                if (data == null)
                {
                    data = new PlayerData
                    {
                        nickname = pendingSignUp && !string.IsNullOrEmpty(pendingNickname)
                            ? pendingNickname
                            : (user.Email ?? "Player")
                    };
                    // 현재 로컬 볼륨을 계정 초기값으로 (첫 로그인에서 기본값으로 덮이지 않게).
                    var am = AudioManager.Instance;
                    if (am != null) { data.bgmVolume = am.BgmVolume; data.sfxVolume = am.SfxVolume; }

                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    PlayerDataService.Instance.Save(data, now, ok =>
                    {
                        if (!ok) SetStatus("초기 데이터 저장 실패.");
                        Finish(data);
                    });
                }
                else
                {
                    Finish(data);
                }
            });
        }

        private void Finish(PlayerData data)
        {
            pendingSignUp = false;
            SetStatus("");
            OnLoginComplete?.Invoke(data);
        }

        private void HandleError(string message)
        {
            pendingSignUp = false;
            SetStatus(message);
        }

        private static string GetText(TMP_InputField field) => field != null ? field.text : "";

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
