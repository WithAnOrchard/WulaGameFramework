# AutoUpdateManager

`AutoUpdateManager`锛坄[Manager(-25)]` 闂ㄩ潰锛変笌 `AutoUpdateService`锛堢姸鎬佹満锛夋彁渚涚増鏈洿鏂拌兘鍔涖€?
鍚姩鏃朵細 GET 杩滅 `latest.json` 涓?`PlayerSettings.bundleVersion` 姣旇緝锛屾敮鎸佹暣鍖呬笅杞?+ 鏈湴瑙ｅ帇鏇存柊銆?
## 鐩綍

```
AutoUpdateManager/
鈹溾攢鈹€ AutoUpdateManager.cs           闂ㄩ潰 [Manager(-25)] + Event 鍏ュ彛
鈹溾攢鈹€ AutoUpdateService.cs           Idle鈫扖hecking鈫扐vailable鈫扗ownloading鈫扗ownloaded鈫扞nstalling
鈹溾攢鈹€ Agent.md                       鏈枃妗?鈹溾攢鈹€ Dao/
鈹?  鈹斺攢鈹€ UpdateManifest.cs          杩滅 manifest DTO + UpdateStage
鈹溾攢鈹€ Runtime/
鈹?  鈹溾攢鈹€ UpdateDownloader.cs        HTTP 寮傛涓嬭浇
鈹?  鈹斺攢鈹€ UpdateInstaller.cs         Windows: PowerShell stub
鈹斺攢鈹€ Editor/
    鈹斺攢鈹€ AutoUpdateBuilder.cs       鐢熸垚 v{version}.zip + latest.json
```

## 閮ㄧ讲渚ф祦绋嬶紙鍑烘柊鐗堬級

1. **鍏堟洿鏂扮増鏈彿**锛氬湪 `Project Settings -> Player` 閲岃缃?`bundleVersion`锛堜緥濡?`1.2.0`锛夈€?2. **鏋勫缓娓告垙**锛氳蛋 `Build/WulaSystem/Foundation/Build Player`锛堟垨宸叉湁鏋勫缓娴佺▼锛夈€?3. **鏋勫缓鍚庤嚜鍔ㄤ骇鐗?*锛歚BuildSystem` 浼氬湪 `OnPostprocessBuild` 闃舵鑷姩鍐欏叆锛?   - `Build/Updates/v1.2.0.zip`
   - `Build/Updates/latest.json`
4. **纭鏇存柊鏈嶅姟鍣?URL**锛歚latest.json` 鍐?`downloadUrl` 濡傞渶鏇挎崲鍒扮湡瀹?CDN 鍦板潃锛屽彲缁х画浣跨敤 `Build/WulaSystem/Foundation/AutoUpdate/Set Update Base URL...`銆?5. **涓婁紶**锛歚rsync/ssh` 涓婁紶 `Build/Updates/` 鍒板搴?CDN 璺緞銆?6. **涓婄嚎**锛氬鎴风涓嬫鍚姩鏃朵細璇锋眰 `latest.json`锛屾娴嬫洿鏂板苟鎻愮ず銆?
鎵嬪姩鏂瑰紡浠嶅彲鐢細
- `Build/WulaSystem/Foundation/AutoUpdate/Build Update Package`锛堥€傚悎鍗曠嫭鎵撳寘鍦烘櫙锛?
## Event API

## 杩愯鏃舵祦绋?
- `AutoUpdateManager` 璐熻矗鎺ュ叆浜嬩欢锛?  - `EVT_CHECK_UPDATE`
  - `EVT_BEGIN_DOWNLOAD`
  - `EVT_BEGIN_INSTALL`
  - `EVT_SKIP_VERSION`
- `AutoUpdateService` 璐熻矗鏇存柊鐘舵€佹満锛屽苟瑙﹀彂
  - `EVT_CHECK_STARTED`
  - `EVT_AVAILABLE`
  - `EVT_DOWNLOAD_PROGRESS`
  - `EVT_DOWNLOADED`
  - `EVT_INSTALLING`
  - `EVT_SKIPPED`
  - `EVT_UP_TO_DATE`
  - `EVT_FAILED`

## 娉ㄦ剰

- `minVersion / mandatory / SHA256 鏍￠獙` 褰撳墠涓哄姛鑳戒綅锛屼富娴佺▼榛樿涓嶅己鍒躲€?- 鏇存柊鍖呴粯璁ら€傞厤 Windows锛堝彂甯冨櫒閲囩敤 PowerShell锛夈€?- `PlayerSettings.bundleVersion` 涓虹┖鏃讹紝鏇存柊鍖呮瀯寤轰細澶辫触骞舵姤閿欙紱杩欎篃鍙敤浜庣害鏉熸瘡娆?Demo 鏋勫缓蹇呴』鎵撶増鏈彿銆?

