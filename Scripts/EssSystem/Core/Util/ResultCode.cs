using System.Collections.Generic;

namespace EssSystem.Core
{
    /// <summary>
    ///     统一结果码 — 替代全局魔法字符串 "成功"/"错误"
    /// </summary>
    public static class ResultCode
    {
        public const string OK    = "成功";
        public const string ERROR = "错误";

        /// <summary>构造成功结果</summary>
        public static List<object> Ok(object data = null) =>
            data != null ? new List<object> { OK, data } : new List<object> { OK };

        /// <summary>构造失败结果</summary>
        public static List<object> Fail(string msg) =>
            new List<object> { ERROR, msg };

        /// <summary>判断结果是否成功</summary>
        public static bool IsOk(List<object> result) =>
            result != null && result.Count >= 1 && result[0] as string == OK;
    }
}
