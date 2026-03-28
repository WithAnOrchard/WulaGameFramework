using System;
using System.Collections.Generic;

namespace CharacterManager.Dao
{
    [Serializable]
    public class CharacterModel : EssSystem.Core.Dao.Dao
    {
        public string id;
        public List<ModelPart> ModelParts = new();
    }

    [Serializable]
    public class ModelPart : EssSystem.Core.Dao.Dao
    {
        public string id;

        //渲染模式: "sprite"=静态Sprite, "animation"=动态Animation
        public string renderMode = "sprite";

        //所有可用样式名称（对应ResourceLoaderManager中的资源名）
        public List<string> styles = new();

        //当前使用的样式下标
        public int currentStyleIndex;

        //动画相关（renderMode="animation"时使用）
        //从路径加载所有png作为sprite序列
        public string spritesPath = "";

        //每个动画状态的定义
        public List<ModelPartState> ModelPartStates = new();

        //渲染层级
        public int layer;
        public int orderInLayer;

        //默认动画状态名
        public string defaultAnimation = "静止";

        //获取当前样式名
        public string GetCurrentStyle()
        {
            if (styles.Count == 0) return "";
            return styles[currentStyleIndex % styles.Count];
        }

        //切换到下一个样式
        public void NextStyle()
        {
            if (styles.Count == 0) return;
            currentStyleIndex = (currentStyleIndex + 1) % styles.Count;
        }

        //切换到上一个样式
        public void PrevStyle()
        {
            if (styles.Count == 0) return;
            currentStyleIndex = (currentStyleIndex - 1 + styles.Count) % styles.Count;
        }
    }

    [Serializable]
    public class ModelPartState : EssSystem.Core.Dao.Dao
    {
        public string id;

        //这一批素材中占多少帧
        public int span;
    }
}