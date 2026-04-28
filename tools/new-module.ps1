# ─────────────────────────────────────────────────────────────────────
# new-module.ps1 — 业务 Manager 脚手架生成器
#
# 用法：
#   .\tools\new-module.ps1 -Name Quest                    # 默认优先级 10
#   .\tools\new-module.ps1 -Name Quest -Priority 15
#   .\tools\new-module.ps1 -Name Quest -Force             # 覆盖已存在的目录
#
# 生成内容（在 Assets/Scripts/EssSystem/Manager/{Name}Manager/ 下）：
#   {Name}Manager.cs       业务 Manager
#   {Name}Service.cs       业务 Service
#   Dao/{Name}Data.cs      数据类示例
#   Agent.md               模块文档骨架
# ─────────────────────────────────────────────────────────────────────

param(
    [Parameter(Mandatory=$true)][string]$Name,
    [int]$Priority = 10,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# 解析项目根：脚本在 Assets/tools/ 下
$projectRoot = Split-Path -Parent $PSScriptRoot
$moduleRoot  = Join-Path $projectRoot "Scripts\EssSystem\Manager\${Name}Manager"
$daoRoot     = Join-Path $moduleRoot 'Dao'

if (Test-Path $moduleRoot) {
    if (-not $Force) {
        Write-Error "目录已存在: $moduleRoot（用 -Force 覆盖）"
        exit 1
    }
    Remove-Item $moduleRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $daoRoot -Force | Out-Null

$utf8 = New-Object System.Text.UTF8Encoding($false)
function Write-Doc($path, $content) {
    [System.IO.File]::WriteAllText($path, $content, $utf8)
    Write-Host "  + $($path.Replace($projectRoot, '...'))"
}

# ─── {Name}Manager.cs ───
Write-Doc "$moduleRoot\${Name}Manager.cs" @"
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;

namespace EssSystem.EssManager.${Name}Manager
{
    /// <summary>${Name} 业务 Manager（薄门面）。</summary>
    [Manager($Priority)]
    public class ${Name}Manager : Manager<${Name}Manager>
    {
        // ─── Event 名常量（供调用方使用，避免魔法字符串）──────────────
        public const string EVT_DO_SOMETHING = "${Name}_DoSomething";

        /// <summary>关联的 Service（自动从单例获取）。</summary>
        public ${Name}Service Service => ${Name}Service.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("${Name}Manager 初始化完成", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        // ─── 事件处理 ─────────────────────────────────────────
        [Event(EVT_DO_SOMETHING)]
        public List<object> DoSomething(List<object> data)
        {
            // TODO: 实现业务
            return ResultCode.Ok();
        }
    }
}
"@

# ─── {Name}Service.cs ───
Write-Doc "$moduleRoot\${Name}Service.cs" @"
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.EssManager.${Name}Manager.Dao;

namespace EssSystem.EssManager.${Name}Manager
{
    /// <summary>${Name} 业务 Service（持久化 + 业务逻辑）。</summary>
    public class ${Name}Service : Service<${Name}Service>
    {
        // ─── 数据分类常量 ────────────────────────────────────
        public const string CAT_DATA = "Data";

        // ─── Event 名常量 ────────────────────────────────────
        public const string EVT_QUERY = "${Name}_Query";

        protected override void Initialize()
        {
            base.Initialize();
            // TODO: 自定义初始化
            Log("${Name}Service 初始化完成", Color.green);
        }

        // ─── Public API ──────────────────────────────────────

        /// <summary>示例：保存一条数据。</summary>
        public void Save(string key, ${Name}Data value) => SetData(CAT_DATA, key, value);

        /// <summary>示例：读取一条数据。</summary>
        public ${Name}Data Load(string key) => GetData<${Name}Data>(CAT_DATA, key);

        // ─── 事件处理 ─────────────────────────────────────────
        [Event(EVT_QUERY)]
        public List<object> Query(List<object> data)
        {
            if (data == null || data.Count < 1) return ResultCode.Fail("参数无效");
            var key = data[0] as string;
            var value = Load(key);
            return value != null ? ResultCode.Ok(value) : ResultCode.Fail("未找到");
        }
    }
}
"@

# ─── Dao/{Name}Data.cs ───
Write-Doc "$daoRoot\${Name}Data.cs" @"
using System;

namespace EssSystem.EssManager.${Name}Manager.Dao
{
    /// <summary>${Name} 模块数据示例（必须 [Serializable] 才能被持久化）。</summary>
    [Serializable]
    public class ${Name}Data
    {
        public string Id;
        public string Name;
        // TODO: 添加业务字段
    }
}
"@

# ─── Agent.md ───
Write-Doc "$moduleRoot\Agent.md" @"
# ${Name}Manager 指南

## 概述

``${Name}Manager``（``[Manager($Priority)]``）+ ``${Name}Service`` 提供 ${Name} 模块的功能。

## 文件结构

``````
${Name}Manager/
├── ${Name}Manager.cs       ← 薄门面，事件入口
├── ${Name}Service.cs       ← 业务逻辑 + 持久化
├── Agent.md                ← 本文档
└── Dao/
    └── ${Name}Data.cs      ← 数据类
``````

## Event 名常量

| 常量 | 值 | 说明 |
|---|---|---|
| ``${Name}Manager.EVT_DO_SOMETHING`` | ``${Name}_DoSomething`` | TODO 描述 |
| ``${Name}Service.EVT_QUERY`` | ``${Name}_Query`` | 查询单条数据 |

## 调用示例

``````csharp
// 调用 Manager 暴露的事件
EventProcessor.Instance.TriggerEventMethod(${Name}Manager.EVT_DO_SOMETHING,
    new List<object> { /* args */ });

// 直接调 Service（内部使用）
${Name}Service.Instance.Save("id", new ${Name}Data { Id = "id", Name = "test" });
``````

## 数据持久化

- 路径：``{persistentDataPath}/ServiceData/${Name}Service/Data.json``
- 由 DataService 在应用退出时自动保存
- 任何 ``SetData`` 调用都会立即保存对应分类

## TODO

- [ ] 实现 ``DoSomething`` 业务逻辑
- [ ] 完善 ``${Name}Data`` 字段
- [ ] 编写单元测试（如有）
"@

Write-Host ''
Write-Host "✅ 模块 '${Name}Manager' 创建成功（优先级 $Priority）"
Write-Host '   下一步：'
Write-Host "   1. 在场景的 GameManager GameObject 上 AddComponent → ${Name}Manager"
Write-Host "   2. 编辑 ${Name}Service.cs 实现业务"
Write-Host "   3. 在 Assets/Agent.md 顶层添加新模块的引用"
