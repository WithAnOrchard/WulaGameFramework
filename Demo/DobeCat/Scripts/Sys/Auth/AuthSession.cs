using System;
using BiliBiliDanmu.Auth;

namespace Demo.DobeCat.Sys.Auth
{
    /// <summary>
    /// DobeCat 登录态（委托到 EssSystem BilibiliAuthSession）。
    /// <para>保留此类以维持 DobeCat 代码兼容性。</para>
    /// </summary>
    public static class AuthSession
    {
        /// <summary>当前 SESSDATA token；未登录时为空串。</summary>
        public static string Token => BilibiliAuthSession.Token;
        /// <summary>登录用户昵称（B 站 uname）。</summary>
        public static string Nickname => BilibiliAuthSession.Nickname;
        /// <summary>登录用户 mid（B 站用户唯一 id）。</summary>
        public static long Mid => BilibiliAuthSession.Mid;

        /// <summary>是否已登录（token 非空）。</summary>
        public static bool IsAuthenticated => BilibiliAuthSession.IsAuthenticated;

        /// <summary>登录成功时触发。</summary>
        public static event Action OnLogin
        {
            add => BilibiliAuthSession.OnLogin += value;
            remove => BilibiliAuthSession.OnLogin -= value;
        }

        /// <summary>设置 token + 用户信息并持久化。</summary>
        public static bool Login(string token, string nickname, long mid)
            => BilibiliAuthSession.Login(token, nickname, mid);

        /// <summary>清除登录态。</summary>
        public static void Logout()
            => BilibiliAuthSession.Logout();
    }
}
