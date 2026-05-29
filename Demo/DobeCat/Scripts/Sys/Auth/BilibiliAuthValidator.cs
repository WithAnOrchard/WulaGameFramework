using System.Collections;
using BiliBiliDanmu.Auth;

namespace Demo.DobeCat.Sys.Auth
{
    /// <summary>
    /// DobeCat B 站验证器（委托到 EssSystem BilibiliAuthValidator）。
    /// <para>保留此类以维持 DobeCat 代码兼容性。</para>
    /// </summary>
    public static class BilibiliAuthValidator
    {
        /// <summary>验证 SESSDATA token，必须从 MonoBehaviour 协程启动。</summary>
        public static System.Collections.IEnumerator Validate(string sessdata, System.Action<string, long> onSuccess, System.Action<string> onFail)
            => BiliBiliDanmu.Auth.BilibiliAuthValidator.Validate(sessdata, onSuccess, onFail);
    }
}
