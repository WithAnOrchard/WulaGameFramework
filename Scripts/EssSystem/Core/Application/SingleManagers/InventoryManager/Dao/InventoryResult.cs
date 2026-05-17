using System;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao
{
    /// <summary>
    /// 背包操作结果 — 统一 Add/Remove/Move 的返回结构。
    /// </summary>
    [Serializable]
    public readonly struct InventoryResult
    {
        public readonly bool   Success;
        public readonly int    Amount;      // 实际操作的数量
        public readonly int    Remaining;   // 剩余未处理数量
        public readonly string Message;

        public InventoryResult(bool success, int amount, int remaining, string message)
        {
            Success = success; Amount = amount; Remaining = remaining; Message = message ?? string.Empty;
        }

        public static InventoryResult Ok(int amount) =>
            new InventoryResult(true, amount, 0, "");

        public static InventoryResult Partial(int amount, int remaining, string msg) =>
            new InventoryResult(true, amount, remaining, msg);

        public static InventoryResult Fail(string msg) =>
            new InventoryResult(false, 0, 0, msg);

        public override string ToString() =>
            Success ? $"成功(+{Amount}, 剩余={Remaining})" : $"失败({Message})";
    }
}
