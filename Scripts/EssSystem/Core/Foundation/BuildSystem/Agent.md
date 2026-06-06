# BuildSystem 鎸囧崡

`Foundation/BuildSystem` 鎻愪緵妗嗘灦绾ф瀯寤鸿緟鍔╄兘鍔涳紝鐩爣鏄妸鈥滄瀯寤烘椂鎬昏鎵嬪伐璺戠殑棰勫鐞嗏€濈粺涓€杩?Unity Build 娴佺▼銆?
## 褰撳墠鑱岃矗
- 涓€閿瀯寤鸿緟鍔╋細`OneClickBuildHelper`
- Unity Build 鑿滃崟鍏ュ彛锛歚Build/WulaSystem/Foundation/Build Player`
- 鏋勫缓鍓嶉澶勭悊锛歚ResourceManifest` 鑷姩鐢熸垚
- 鏋勫缓鍚庤嚜鍔ㄧ敓鎴愶細`AutoUpdate` 鍙戝竷浜х墿锛坄Build/Updates/v{version}.zip` + `Build/Updates/latest.json`锛?- 寮€鍏虫帶鍒讹細`Build/WulaSystem/Foundation/AutoUpdate/Auto Generate Update Package After Build`锛堥粯璁ゅ紑鍚級

## 鏂囦欢缁撴瀯

```
BuildSystem/
鈹溾攢鈹€ OneClickBuildHelper.cs             蹇€熸瀯寤鸿緟鍔╋紝鏀寔 Addressables
鈹斺攢鈹€ Editor/
    鈹溾攢鈹€ EssSystemBuildMenu.cs          Unity Build 鑿滃崟鍏ュ彛
    鈹斺攢鈹€ EssSystemBuildPreprocessor.cs  Preprocess + Postprocess 鏋勫缓閽╁瓙
```

## 鏋勫缓娴佺▼

`BuildPipeline.BuildPlayer`
    -> `EssSystemBuildPreprocessor.OnPreprocessBuild`
    -> `ResourceManifestGenerator.Generate()`
    -> Unity 瀹樻柟 Build
    -> `EssSystemBuildPreprocessor.OnPostprocessBuild`
    -> `AutoUpdateBuilder.BuildFromBuildOutput()`
    -> 鐢熸垚 `Build/Updates/v{bundleVersion}.zip` 涓?`Build/Updates/latest.json`

`callbackOrder = -100`锛屼紭鍏堜簬涓氬姟椤圭洰鐨勬瀯寤洪澶勭悊杩愯銆?
## 涓氬姟寤鸿

- 姣忔鏋勫缓 Demo 鐗堟湰鍓嶏紝鍏堟洿鏂?`Project Settings -> Player -> Version`锛堝搴?`PlayerSettings.bundleVersion`锛夈€?- `AutoUpdate` 鐨勬洿鏂板寘鍚嶄細鎸?`v{bundleVersion}.zip` 鐢熸垚锛屽拰 manifest 鐨?`version` 涓€鑷达紝渚夸簬鐩存帴鎶曟斁鍒板搴旀湇鍔＄鐩綍銆?- 榛樿鍙湪 Windows 鏋勫缓鏃惰嚜鍔ㄧ敓鎴愭洿鏂板寘锛涘叾浠栧钩鍙拌嫢涓嶆敮鎸佸彲淇濈暀鎵嬪姩娴佺▼銆?
## OneClickBuildHelper

`OneClickBuildHelper.QuickBuild(...)` 浠嶆寜鐜版湁鏂瑰紡宸ヤ綔锛?1. 鏍￠獙鍦烘櫙鏂囦欢璺緞
2. 鍙€夊叧闂寚瀹?Addressables 鍒嗙粍 `IncludeInBuild`
3. 鎵ц Addressables 鎵撳寘
4. 璋冪敤 `BuildPipeline.BuildPlayer`
5. 鎭㈠鏋勫缓璁剧疆骞舵墦鍗版姤鍛?
## 娉ㄦ剰浜嬮」

- `EssSystemBuildPreprocessor` 涓?`AutoUpdateBuilder` 閮藉湪 `#if UNITY_EDITOR` 涓嬬紪璇戙€?- 濡傛灉浣犵殑 AutoUpdate 鍙戝竷娴佺▼瑕佹敼涓烘墜鍔紝浠嶅彲璋冪敤鏃х殑 `Build/WulaSystem/Foundation/AutoUpdate/Build Update Package` 鑿滃崟銆?- `Build/WulaSystem/Foundation/AutoUpdate/Auto Generate Update Package After Build` 榛樿寮€鍚紱鍏抽棴鍚庢瀯寤轰粎浜у嚭 Player锛屼笉浼氳嚜鍔ㄧ敓鎴?`Build/Updates/*`锛屼綘鍙墜鍔ㄦ墦鍖呮洿鏂板寘銆?

