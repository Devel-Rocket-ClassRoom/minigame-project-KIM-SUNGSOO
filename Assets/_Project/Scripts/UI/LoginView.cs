using System;
using Firebase.Auth;
using KRTD.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 이메일/비밀번호 로그인 "폼" 패널. 패널 전환(로그인↔메뉴)은 <see cref="MainMenuView"/> 가 담당하고,
    /// 이 컴포넌트는 입력 → 인증 → 데이터 로드까지만 책임진 뒤 <see cref="OnLoginComplete"/> 로 결과를 알린다.
    ///
    /// 흐름:
    ///   로그인/회원가입 버튼 → AuthManager 호출 → 성공(OnSignedIn) → CloudSaveManager.Load
    ///   → (신규면 PlayerData 생성·저장) → OnLoginComplete(data) 발사 → MainMenuView 가 메뉴로 전환.
    ///
    /// 권장 구조 (이 컴포넌트는 loginPanel 루트에 부착):
    ///   LoginPanel (이 컴포넌트)
    ///   ├─ EmailInput     (TMP_InputField)
    ///   ├─ PasswordInput  (TMP_InputField, Content Type = Password)
    ///   ├─ NicknameInput  (TMP_InputField, 회원가입용)
    ///   ├─ LoginButton    "로그인"
    ///   ├─ SignUpButton   "회원가입"
    ///   └─ StatusText     (TMP_Text)  안내/에러 메시지
    ///
    /// 의존: 씬 어딘가에 FirebaseInit / AuthManager / CloudSaveManager 가 있어야 한다.
    /// </summary>
    public class LoginView : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [Tooltip("회원가입 시 사용할 닉네임. 로그인에는 사용하지 않음.")]
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button signUpButton;

        [Header("표시")]
        [SerializeField] private TMP_Text statusText;

        /// <summary>로그인(또는 세션 복원) + 데이터 로드/생성까지 끝났을 때 PlayerData 와 함께 발사.
        /// MainMenuView 가 구독해 메뉴 패널로 전환한다.</summary>
        public event Action<PlayerData> OnLoginComplete;

        // 회원가입 흐름인지 구분 — 신규 계정일 때 닉네임을 데이터에 심기 위해 보관.
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
            }

            // 앱 시작 시 이미 세션이 살아있으면 안내만 표시(곧 OnSignedIn 이 와서 자동 진입).
            if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
                SetStatus("자동 로그인 중...");
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

        // --- 버튼 핸들러 -------------------------------------------------------

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

        // --- 인증 이벤트 -------------------------------------------------------

        private void HandleSignedIn(FirebaseUser user)
        {
            SetStatus("데이터 불러오는 중...");

            if (CloudSaveManager.Instance == null)
            {
                SetStatus("저장 시스템이 준비되지 않았습니다.");
                return;
            }

            CloudSaveManager.Instance.Load(data =>
            {
                if (data == null)
                {
                    // 신규 계정 → 새 데이터 생성 후 저장.
                    data = new PlayerData
                    {
                        nickname = pendingSignUp && !string.IsNullOrEmpty(pendingNickname)
                            ? pendingNickname
                            : (user.Email ?? "Player")
                    };
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    CloudSaveManager.Instance.Save(data, now, ok =>
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

        // --- 유틸 --------------------------------------------------------------

        private static string GetText(TMP_InputField field) => field != null ? field.text : "";

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
