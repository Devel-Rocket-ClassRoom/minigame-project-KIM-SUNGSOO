using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// Firebase 이메일/비밀번호 인증 래퍼 싱글톤.
    /// <see cref="DontDestroyOnLoad"/> 로 씬 전환에도 살아남으며 로그인 상태를 유지한다.
    ///
    /// 책임:
    ///   - 회원가입(<see cref="SignUp"/>) / 로그인(<see cref="SignIn"/>) / 로그아웃(<see cref="SignOut"/>)
    ///   - Firebase 의 <see cref="FirebaseAuth.StateChanged"/> 를 구독해 로그인/로그아웃 이벤트 브로드캐스트
    ///   - 에러 코드를 한국어 메시지로 변환해 UI 에 전달
    ///
    /// 주의:
    ///   - <see cref="FirebaseInit"/> 초기화가 끝나야 동작한다. 준비 전이면 OnReady 를 기다린다.
    ///   - Firebase 콘솔 → Authentication → Sign-in method 에서 "이메일/비밀번호" 를 활성화해야 한다.
    ///   - 모든 콜백은 ContinueWithOnMainThread 로 메인 스레드에서 돈다 → Unity API 안전.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        private FirebaseAuth auth;

        /// <summary>현재 로그인된 사용자. 미로그인 시 null.</summary>
        public FirebaseUser CurrentUser => auth?.CurrentUser;
        public bool IsSignedIn => CurrentUser != null;
        /// <summary>Realtime Database 경로 키로 쓰는 고유 ID. 미로그인 시 null.</summary>
        public string UserId => CurrentUser?.UserId;

        /// <summary>로그인 성공(또는 앱 시작 시 기존 세션 복원) 시 호출.</summary>
        public event Action<FirebaseUser> OnSignedIn;
        /// <summary>로그아웃 시 호출.</summary>
        public event Action OnSignedOut;
        /// <summary>회원가입/로그인 실패 시 한국어 메시지와 함께 호출.</summary>
        public event Action<string> OnError;

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

            if (FirebaseInit.IsReady) Setup();
            else FirebaseInit.OnReady += Setup;
        }

        private void OnDestroy()
        {
            FirebaseInit.OnReady -= Setup;
            if (auth != null) auth.StateChanged -= HandleStateChanged;
            if (Instance == this) Instance = null;
        }

        private void Setup()
        {
            auth = FirebaseAuth.DefaultInstance;
            auth.StateChanged += HandleStateChanged;
            // 앱 시작 시 이미 로그인된 세션이 있으면 즉시 한 번 반영.
            HandleStateChanged(this, EventArgs.Empty);
        }

        private FirebaseUser lastUser;

        private void HandleStateChanged(object sender, EventArgs e)
        {
            FirebaseUser user = auth.CurrentUser;
            if (user == lastUser) return; // 중복 방지.
            lastUser = user;

            if (user != null) OnSignedIn?.Invoke(user);
            else OnSignedOut?.Invoke();
        }

        // --- 공개 API ----------------------------------------------------------

        /// <summary>이메일/비밀번호로 새 계정 생성. 성공 시 자동 로그인되어 OnSignedIn 이 발사된다.</summary>
        public void SignUp(string email, string password)
        {
            if (!ValidateInput(email, password)) return;
            if (auth == null) { OnError?.Invoke("Firebase 준비 중입니다. 잠시 후 다시 시도하세요."); return; }

            auth.CreateUserWithEmailAndPasswordAsync(email.Trim(), password)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        OnError?.Invoke(DescribeError(task.Exception));
                        return;
                    }
                    // 성공 — StateChanged 가 OnSignedIn 을 발사한다.
                });
        }

        /// <summary>기존 계정으로 로그인. 성공 시 OnSignedIn 이 발사된다.</summary>
        public void SignIn(string email, string password)
        {
            if (!ValidateInput(email, password)) return;
            if (auth == null) { OnError?.Invoke("Firebase 준비 중입니다. 잠시 후 다시 시도하세요."); return; }

            auth.SignInWithEmailAndPasswordAsync(email.Trim(), password)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        OnError?.Invoke(DescribeError(task.Exception));
                        return;
                    }
                    // 성공 — StateChanged 가 OnSignedIn 을 발사한다.
                });
        }

        /// <summary>로그아웃. StateChanged 가 OnSignedOut 을 발사한다.</summary>
        public void SignOut()
        {
            auth?.SignOut();
        }

        // --- 내부 유틸 ----------------------------------------------------------

        private bool ValidateInput(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                OnError?.Invoke("이메일을 입력하세요.");
                return false;
            }
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                OnError?.Invoke("비밀번호는 6자 이상이어야 합니다.");
                return false;
            }
            return true;
        }

        /// <summary>Firebase 에러를 사용자에게 보여줄 한국어 메시지로 변환.</summary>
        private string DescribeError(AggregateException exception)
        {
            if (exception == null) return "알 수 없는 오류가 발생했습니다.";

            foreach (var inner in exception.Flatten().InnerExceptions)
            {
                if (inner is FirebaseException fe)
                {
                    return ((AuthError)fe.ErrorCode) switch
                    {
                        AuthError.MissingEmail => "이메일을 입력하세요.",
                        AuthError.MissingPassword => "비밀번호를 입력하세요.",
                        AuthError.InvalidEmail => "이메일 형식이 올바르지 않습니다.",
                        AuthError.WeakPassword => "비밀번호가 너무 약합니다. (6자 이상)",
                        AuthError.EmailAlreadyInUse => "이미 사용 중인 이메일입니다.",
                        AuthError.WrongPassword => "비밀번호가 일치하지 않습니다.",
                        AuthError.UserNotFound => "등록되지 않은 계정입니다.",
                        AuthError.UserDisabled => "비활성화된 계정입니다.",
                        AuthError.TooManyRequests => "시도가 너무 많습니다. 잠시 후 다시 시도하세요.",
                        AuthError.NetworkRequestFailed => "네트워크 연결을 확인하세요.",
                        _ => $"인증 오류: {fe.Message}"
                    };
                }
            }
            return "오류가 발생했습니다. 다시 시도하세요.";
        }
    }
}
