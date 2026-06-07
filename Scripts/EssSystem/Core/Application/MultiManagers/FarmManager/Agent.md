# FarmManager 农场模块

## 职责
- 负责作物状态、成长阶段、收获和农田数据。
- 模块路径：`Scripts/EssSystem/Core/Application/MultiManagers/FarmManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Dao/`
- `FarmManager.cs`
- `FarmService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `FarmManager.EVT_CLEAR_SLOT` = `"ClearFarmSlot"`
- `FarmManager.EVT_FERTILIZE` = `"FertilizeCrop"`
- `FarmManager.EVT_HARVEST_CROP` = `"HarvestCrop"`
- `FarmManager.EVT_PLANT_CROP` = `"PlantCrop"`
- `FarmManager.EVT_QUERY_SLOT` = `"QueryFarmSlot"`
- `FarmManager.EVT_REGISTER_CROP_CONFIG` = `"RegisterCropConfig"`
- `FarmManager.EVT_REGISTER_FARM_CONFIG` = `"RegisterFarmConfig"`
- `FarmManager.EVT_REMOVE_PEST` = `"RemovePest"`
- `FarmManager.EVT_SPAWN_FARM` = `"SpawnFarm"`
- `FarmManager.EVT_WATER_CROP` = `"WaterCrop"`
- `FarmService.EVT_ON_CROP_FERTILIZED` = `"OnCropFertilized"`
- `FarmService.EVT_ON_CROP_HARVESTED` = `"OnCropHarvested"`
- `FarmService.EVT_ON_CROP_PLANTED` = `"OnCropPlanted"`
- `FarmService.EVT_ON_CROP_STAGE_CHANGED` = `"OnCropStageChanged"`
- `FarmService.EVT_ON_CROP_WATERED` = `"OnCropWatered"`
- `FarmService.EVT_ON_CROP_WILTED` = `"OnCropWilted"`
- `FarmService.EVT_ON_FARM_SPAWNED` = `"OnFarmSpawned"`
- `FarmService.EVT_ON_PEST_REMOVED` = `"OnPestRemoved"`
- `FarmService.EVT_ON_PEST_SPAWNED` = `"OnPestSpawned"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
