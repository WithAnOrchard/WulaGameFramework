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
        private const string PrefKey     = "DobeCat.AuthToken";
        private const string PrefUname   = "DobeCat.AuthUname";
        private const string PrefMid     = "DobeCat.AuthMid";

        /// <summary>当前 token；未登录时为空串。</summary>
        public static string Token { get; private set; }
        /// <summary>登录用户昵称（B 站 uname）。</summary>
        public static string Nickname { get; private set; }
        /// <summary>登录用户 mid（B 站用户唯一 id）。</summary>
        public static long Mid { get; private set; }

        /// <summary>是否已登录（token 非空）。注意：不代表 token 仍然有效，需要再走一次 <see cref="BilibiliAuthValidator"/>。</summary>
        public static bool IsAuthenticated => !string.IsNullOrEmpty(Token);

        /// <summary>登录成功时触发（不论是启动期自动还是用户手动）。</summary>
        public static event Action OnLogin;

        static AuthSession()
        {
            Token    = PlayerPrefs.GetString(PrefKey,   string.Empty);
            Nickname = PlayerPrefs.GetString(PrefUname, string.Empty);
            // PlayerPrefs 不存 long，用 string 存
            long.TryParse(PlayerPrefs.GetString(PrefMid, "0"), out var mid);
            Mid = mid;
        }

        /// <summary>设置 token + 用户信息并持久化；空 token 视为无效，不触发事件。</summary>
        public static bool Login(string token, string nickname, long mid)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            Token    = token.Trim();
            Nickname = nickname ?? string.Empty;
            Mid      = mid;
            PlayerPrefs.SetString(PrefKey,   Token);
            PlayerPrefs.SetString(PrefUname, Nickname);
            PlayerPrefs.SetString(PrefMid,   Mid.ToString());
            PlayerPrefs.Save();
            try { OnLogin?.Invoke(); } catch { /* swallow */ }
            return true;
        }

        /// <summary>清除登录态（不会自动跳转到登录界面，由调用方决定）。</summary>
        public static void Logout()
        {
            Token = string.Empty;
            Nickname = string.Empty;
            Mid = 0;
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.DeleteKey(PrefUname);
            PlayerPrefs.DeleteKey(PrefMid);
            PlayerPrefs.Save();
        }
    }
}
