using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.DialogueManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.DialogueManager.Dao.UIConfig;

namespace EssSystem.Core.EssManagers.Gameplay.DialogueManager
{
    /// <summary>
    /// 对话业务服务 — 持久化 <see cref="Dialogue"/> / <see cref="DialogueConfig"/>，并维护当前会话状态机。
    /// <para>
    /// 状态机仅保留单条「活动会话」（绝大多数 RPG 同时只有一个对话框），切换到新对话会自动覆盖。<br/>
    /// 状态本身**不写盘**（对话进度属运行时数据），但 Dialogue / Config 持久化。
    /// </para>
    /// </summary>
    public class DialogueService : Service<DialogueService>
    {
        #region 数据分类

        public const string CAT_DIALOGUES = "Dialogues";
        public const string CAT_CONFIGS   = "Configs";

        #endregion

        #region 事件名称

        // 命令类
        public const string EVT_REGISTER_DIALOGUE = "RegisterDialogue";
        public const string EVT_REGISTER_CONFIG   = "RegisterDialogueConfig";
        public const string EVT_ADVANCE           = "AdvanceDialogue";
        public const string EVT_SELECT_OPTION     = "SelectDialogueOption";
        public const string EVT_END               = "EndDialogue";
        public const string EVT_QUERY_CURRENT     = "QueryDialogueCurrent";

        // 广播类
        public const string EVT_STARTED      = "OnDialogueStarted";
        public const string EVT_LINE_CHANGED = "OnDialogueLineChanged";
        public const string EVT_ENDED        = "OnDialogueEnded";

        #endregion

        // ─── 活动会话状态（运行时，不持久化） ───
        private string _activeDialogueId;
        private string _activeLineId;
        private string _activeConfigId;

        public string ActiveDialogueId => _activeDialogueId;
        public string ActiveLineId     => _activeLineId;
        public string ActiveConfigId   => _activeConfigId;
        public bool   IsActive         => !string.IsNullOrEmpty(_activeDialogueId);

        protected override void Initialize()
        {
            base.Initialize();
            Log("DialogueService 初始化完成", Color.green);
        }

        // ───────────────────────────────────────────
        #region Registration

        /// <summary>注册一段对话（按 Id 覆盖）。</summary>
        public void RegisterDialogue(Dialogue dialogue)
        {
            if (dialogue == null || string.IsNullOrEmpty(dialogue.Id))
            {
                LogWarning("忽略空 Dialogue 或缺 Id");
                return;
            }
            SetData(CAT_DIALOGUES, dialogue.Id, dialogue);
            Log($"注册 Dialogue: {dialogue.Id} ({dialogue.Name})", Color.blue);
        }

        /// <summary>注册一份 UI 配置（按 ConfigId 覆盖）。</summary>
        public void RegisterConfig(DialogueConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空 DialogueConfig 或缺 ConfigId");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册 DialogueConfig: {config.ConfigId}", Color.blue);
        }

        public Dialogue GetDialogue(string id) =>
            string.IsNullOrEmpty(id) ? null : GetData<Dialogue>(CAT_DIALOGUES, id);

        public DialogueConfig GetConfig(string id) =>
            string.IsNullOrEmpty(id) ? null : GetData<DialogueConfig>(CAT_CONFIGS, id);

        public IEnumerable<Dialogue> GetAllDialogues()
        {
            foreach (var key in GetKeys(CAT_DIALOGUES))
            {
                var d = GetDialogue(key);
                if (d != null) yield return d;
            }
        }

        #endregion

        // ───────────────────────────────────────────
        #region Session Control

        /// <summary>开启一段对话；若已有活动会话则直接覆盖。</summary>
        public bool StartDialogue(string dialogueId, string configId = null)
        {
            var dialogue = GetDialogue(dialogueId);
            if (dialogue == null)
            {
                LogWarning($"对话不存在: {dialogueId}");
                return false;
            }
            var first = dialogue.GetFirstLine();
            if (first == null)
            {
                LogWarning($"对话 {dialogueId} 无任何行");
                return false;
            }

            _activeDialogueId = dialogueId;
            _activeLineId     = first.Id;
            _activeConfigId   = !string.IsNullOrEmpty(configId)
                ? configId
                : (!string.IsNullOrEmpty(dialogue.ConfigId) ? dialogue.ConfigId : dialogueId);

            Log($"开始对话: {dialogueId} → {_activeLineId}", Color.cyan);

            EventProcessor.Instance.TriggerEvent(EVT_STARTED,
                new List<object> { _activeDialogueId, _activeConfigId });
            EventProcessor.Instance.TriggerEvent(EVT_LINE_CHANGED,
                new List<object> { _activeDialogueId, _activeLineId });

            return true;
        }

        /// <summary>推进到下一行（仅在当前行无选项时有效）。返回是否仍在对话中。</summary>
        public bool Advance()
        {
            if (!IsActive) return false;
            var dialogue = GetDialogue(_activeDialogueId);
            var line = dialogue?.GetLine(_activeLineId);
            if (line == null) { EndDialogue(); return false; }

            if (line.HasOptions)
            {
                LogWarning("当前行有选项，不能直接 Advance —— 请使用 SelectOption");
                return true;
            }

            DialogueLine next = !string.IsNullOrEmpty(line.NextLineId)
                ? dialogue.GetLine(line.NextLineId)
                : dialogue.GetNextLineInList(line.Id);

            if (next == null) { EndDialogue(); return false; }

            _activeLineId = next.Id;
            EventProcessor.Instance.TriggerEvent(EVT_LINE_CHANGED,
                new List<object> { _activeDialogueId, _activeLineId });
            return true;
        }

        /// <summary>选择当前行的第 <paramref name="index"/> 个选项。</summary>
        public bool SelectOption(int index)
        {
            if (!IsActive) return false;
            var dialogue = GetDialogue(_activeDialogueId);
            var line = dialogue?.GetLine(_activeLineId);
            if (line == null || !line.HasOptions || index < 0 || index >= line.Options.Count)
            {
                LogWarning($"选项索引非法: {index}");
                return false;
            }

            var opt = line.Options[index];

            // 1) 广播事件
            if (!string.IsNullOrEmpty(opt.EventName))
            {
                var args = new List<object>();
                if (opt.EventArgs != null) args.AddRange(opt.EventArgs);
                EventProcessor.Instance.TriggerEvent(opt.EventName, args);
            }

            // 2) 运行时回调
            try { opt.OnSelected?.Invoke(); }
            catch (Exception ex) { LogError($"DialogueOption 回调异常: {ex.Message}"); }

            // 3) 跳转
            return ApplyOptionTransition(opt);
        }

        private bool ApplyOptionTransition(DialogueOption opt)
        {
            var hasNextDialogue = !string.IsNullOrEmpty(opt.NextDialogueId);
            var hasNextLine     = !string.IsNullOrEmpty(opt.NextLineId);

            if (!hasNextDialogue && !hasNextLine)
            {
                EndDialogue();
                return false;
            }

            if (hasNextDialogue)
            {
                var next = GetDialogue(opt.NextDialogueId);
                if (next == null) { LogWarning($"目标对话不存在: {opt.NextDialogueId}"); EndDialogue(); return false; }

                _activeDialogueId = opt.NextDialogueId;
                _activeLineId = hasNextLine ? opt.NextLineId : next.GetFirstLine()?.Id;
                if (string.IsNullOrEmpty(_activeLineId)) { EndDialogue(); return false; }
                _activeConfigId = !string.IsNullOrEmpty(next.ConfigId) ? next.ConfigId : _activeConfigId;
            }
            else // 仅同对话内跳转
            {
                var dialogue = GetDialogue(_activeDialogueId);
                if (dialogue == null || dialogue.GetLine(opt.NextLineId) == null)
                {
                    LogWarning($"目标行不存在: {opt.NextLineId}");
                    EndDialogue();
                    return false;
                }
                _activeLineId = opt.NextLineId;
            }

            EventProcessor.Instance.TriggerEvent(EVT_LINE_CHANGED,
                new List<object> { _activeDialogueId, _activeLineId });
            return true;
        }

        /// <summary>结束当前会话（幂等）。</summary>
        public void EndDialogue()
        {
            if (!IsActive) return;
            var endedId = _activeDialogueId;
            _activeDialogueId = null;
            _activeLineId = null;
            _activeConfigId = null;

            Log($"结束对话: {endedId}", Color.cyan);
            EventProcessor.Instance.TriggerEvent(EVT_ENDED, new List<object> { endedId });
        }

        #endregion

        // ───────────────────────────────────────────
        #region Event Handlers

        /// <summary>注册对话。args: [Dialogue]</summary>
        [Event(EVT_REGISTER_DIALOGUE)]
        public List<object> RegisterDialogueEvent(List<object> args)
        {
            if (args == null || args.Count < 1 || args[0] is not Dialogue d)
                return ResultCode.Fail("参数错误：需要 [Dialogue]");
            RegisterDialogue(d);
            return ResultCode.Ok(d.Id);
        }

        /// <summary>注册对话配置。args: [DialogueConfig]</summary>
        [Event(EVT_REGISTER_CONFIG)]
        public List<object> RegisterConfigEvent(List<object> args)
        {
            if (args == null || args.Count < 1 || args[0] is not DialogueConfig c)
                return ResultCode.Fail("参数错误：需要 [DialogueConfig]");
            RegisterConfig(c);
            return ResultCode.Ok(c.ConfigId);
        }

        /// <summary>推进。args: 无</summary>
        [Event(EVT_ADVANCE)]
        public List<object> AdvanceEvent(List<object> args)
        {
            if (!IsActive) return ResultCode.Fail("没有活动对话");
            Advance();
            return ResultCode.Ok(_activeLineId);
        }

        /// <summary>选择选项。args: [int index]</summary>
        [Event(EVT_SELECT_OPTION)]
        public List<object> SelectOptionEvent(List<object> args)
        {
            if (args == null || args.Count < 1) return ResultCode.Fail("缺 index 参数");
            try
            {
                var idx = Convert.ToInt32(args[0]);
                return SelectOption(idx)
                    ? ResultCode.Ok(_activeLineId)
                    : ResultCode.Fail("选项处理失败或对话已结束");
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }

        /// <summary>结束会话。args: 无</summary>
        [Event(EVT_END)]
        public List<object> EndEvent(List<object> args)
        {
            EndDialogue();
            return ResultCode.Ok("ended");
        }

        /// <summary>查询当前会话。返回 [dialogueId, lineId, configId]，无活动会话返回 Fail。</summary>
        [Event(EVT_QUERY_CURRENT)]
        public List<object> QueryCurrentEvent(List<object> args)
        {
            if (!IsActive) return ResultCode.Fail("无活动对话");
            var ok = ResultCode.Ok();
            ok.Add(_activeDialogueId);
            ok.Add(_activeLineId);
            ok.Add(_activeConfigId);
            return ok;
        }

        #endregion
    }
}
