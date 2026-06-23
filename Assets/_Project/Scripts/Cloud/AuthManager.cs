using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// Firebase 이메일/비밀번호 인증 래퍼 싱글톤. FirebaseInit 준비 후 동작하며
    /// StateChanged 로 로그인/로그아웃을 브로드캐스트한다.
    /// (콘솔: Authentication → 이메일/비밀번호 제공자 활성화 필요)
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        private FirebaseAuth auth;
        private FirebaseUser lastUser;

        public FirebaseUser CurrentUser => auth?.CurrentUser;
        public bool IsSignedIn => CurrentUser != null;
        public string UserId => CurrentUser?.UserId;

        public event Action<FirebaseUser> OnSignedIn;
        public event Action OnSignedOut;
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
            HandleStateChanged(this, EventArgs.Empty); // 기존 세션 즉시 반영
        }

        private void HandleStateChanged(object sender, EventArgs e)
        {
            FirebaseUser user = auth.CurrentUser;
            if (user == lastUser) return;
            lastUser = user;

            if (user != null) OnSignedIn?.Invoke(user);
            else OnSignedOut?.Invoke();
        }

        public void SignUp(string email, string password)
        {
            if (!ValidateInput(email, password)) return;
            if (auth == null) { OnError?.Invoke("Firebase 준비 중입니다. 잠시 후 다시 시도하세요."); return; }

            auth.CreateUserWithEmailAndPasswordAsync(email.Trim(), password)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted) OnError?.Invoke(DescribeError(task.Exception));
                    // 성공 시 StateChanged 가 OnSignedIn 발사
                });
        }

        public void SignIn(string email, string password)
        {
            if (!ValidateInput(email, password)) return;
            if (auth == null) { OnError?.Invoke("Firebase 준비 중입니다. 잠시 후 다시 시도하세요."); return; }

            auth.SignInWithEmailAndPasswordAsync(email.Trim(), password)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted) OnError?.Invoke(DescribeError(task.Exception));
                });
        }

        public void SignOut() => auth?.SignOut();

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
