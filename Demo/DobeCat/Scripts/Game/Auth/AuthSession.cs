using System;
using UnityEngine;

namespace Demo.DobeCat.Game.Auth
{
    /// <summary>
    /// 当前应用登录态。M1 版本仅支持 token：
    /// <list type="bullet">
    /// <item>持久化到 <see cref="PlayerPrefs"/>（Key=<c>DobeCat.AuthToken</c>），下次启动若已存在则跳过登录界面。</item>
    /// <item>外部通过 <see cref="OnLogin"/> 事件得知登录完成。</item>
    /// </list>
    /// 后续可扩展账号/密码、OAuth、SSO 等登录方式；保持 API 不变即可。
    /// </summary>
    public static class AuthSession
    {
        private const string PrefKey = "DobeCat.AuthToken";

        /// <summary>当前 token；未登录时为空串。</summary>
        public static string Token { get; private set; }

        /// <summary>是否已登录（token 非空）。</summary>
        public static bool IsAuthenticated => !string.IsNullOrEmpty(Token);

        /// <summary>登录成功时触发（不论是启动期自动还是用户手动）。</summary>
        public static event Action OnLogin;

        static AuthSession()
        {
            Token = PlayerPrefs.GetString(PrefKey, string.Empty);
        }

        /// <summary>设置 token 并持久化；空串视为无效，不触发事件。</summary>
        public static bool Login(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            Token = token.Trim();
            PlayerPrefs.SetString(PrefKey, Token);
            PlayerPrefs.Save();
            try { OnLogin?.Invoke(); } catch { /* swallow */ }
            return true;
        }

        /// <summary>清除登录态（不会自动跳转到登录界面，由调用方决定）。</summary>
        public static void Logout()
        {
            Token = string.Empty;
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }
    }
}
