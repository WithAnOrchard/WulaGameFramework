using UnityEngine;

namespace Demo.Cubic.Utils
{
    /// <summary>
    /// Cubic 3D 伪 2D 公共风格工具 —— 低多边形 stylized 美术规范。
    /// <para>
    /// 所有"业务自建 cube"统一走这里：建低多边形 Cube（共享 UnityEngine primitive 网格）、
    /// 套 URP/Lit 材质、按职业色染色。避免每个 Spawner 各写一套"创 cube + 上材质"的样板。
    /// </para>
    /// <para>
    /// 风格基线：URP/Lit + Albedo=职业色，无 NormalMap，无 Emission。
    /// 走框架 LightManager / 场景 Directional Light 受光；阴影默认开。
    /// </para>
    /// </summary>
    public static class Cubic3DStyle
    {
        // 共享 primitive cube 网格（Unity 自带 Cube.fbx 的共享引用，多实体复用不占额外内存）
        private static Mesh _sharedCubeMesh;

        // 共享材质实例缓存：color → material。同一职业色共享一个 Material，避免每个实体 new 一份。
        private static readonly System.Collections.Generic.Dictionary<Color, Material> _materialCache =
            new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>获取 / 创建低多边形 cube 共享 Mesh。</summary>
        public static Mesh GetCubeMesh()
        {
            if (_sharedCubeMesh == null)
            {
                // PrimitiveType.Cube 在 GameObject.CreatePrimitive(Cube) 时 Unity 会自动建；
                // 这里用 GameObject.CreatePrimitive 拿一次 mesh 再销毁 GO，避免依赖 Resources。
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _sharedCubeMesh = probe.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Object.Destroy(probe); else Object.DestroyImmediate(probe);
            }
            return _sharedCubeMesh;
        }

        /// <summary>
        /// 创建低多边形 cube GameObject：MeshFilter + MeshRenderer（无 collider，无 rigidbody）。
        /// </summary>
        /// <param name="emissive">true 时材质开启 <c>_EMISSION</c> keyword 并把 <c>_EmissionColor</c> 设为 <paramref name="color"/> × 2（HDR），
        /// 让 URP Bloom override 能采到亮度。用于篝火/水晶/魔法特效这类需要"真发光"的视觉。</param>
        public static GameObject CreateLowPolyCube(string name, Color color, Vector3 scale, bool emissive = false)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = GetCubeMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetLitMaterial(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            go.transform.localScale = scale;

            if (emissive && mr.sharedMaterial != null)
            {
                var mat = mr.sharedMaterial;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2f);
                mat.EnableKeyword("_EMISSION");
            }

            return go;
        }

        /// <summary>把现有 MeshRenderer 染成职业色（无 renderer 时返回 false）。</summary>
        public static bool ApplyJobColor(Renderer r, Color color)
        {
            if (r == null) return false;
            r.sharedMaterial = GetLitMaterial(color);
            return true;
        }

        /// <summary>获取 URP/Lit 材质（按 color 缓存）。找不到 URP/Lit 时回退到 Standard / Unlit/Color。</summary>
        public static Material GetLitMaterial(Color color)
        {
            if (_materialCache.TryGetValue(color, out var cached) && cached != null) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[Cubic3DStyle] 找不到 URP/Lit / Standard / Unlit/Color 任一可用 shader，材质创建失败");
                return null;
            }

            var mat = new Material(shader) { name = $"Cubic_Lit_{ColorUtility.ToHtmlStringRGB(color)}" };
            // URP/Lit 与 Standard 都用 _BaseColor / _Color 入口（URP 是 _BaseColor，Standard 是 _Color；两个都设最稳）
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
            // 低多边形不需要高光，metallic=0, smoothness=0 保持漫反射观感
            if (mat.HasProperty("_Metallic"))  mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);

            _materialCache[color] = mat;
            return mat;
        }

        /// <summary>清空材质缓存（场景切换 / 退出 Play 模式时调，避免脏引用）。</summary>
        public static void ClearCache()
        {
            foreach (var mat in _materialCache.Values)
                if (mat != null) Object.Destroy(mat);
            _materialCache.Clear();
        }
    }
}
