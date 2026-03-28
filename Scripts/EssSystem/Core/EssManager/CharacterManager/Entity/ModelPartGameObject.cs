using CharacterManager.Dao;
using EssSystem.Core;
using UnityEngine;

namespace CharacterManager.Entity
{
    public class ModelPartGameObject : MonoBehaviour
    {
        public ModelPart modelPartData;
        public SpriteRenderer spriteRenderer;
        public Animator animator;

        //切换到下一个样式并刷新显示
        public void SwitchNextStyle()
        {
            if (modelPartData == null) return;
            modelPartData.NextStyle();
            RefreshDisplay();
        }

        //切换到上一个样式并刷新显示
        public void SwitchPrevStyle()
        {
            if (modelPartData == null) return;
            modelPartData.PrevStyle();
            RefreshDisplay();
        }

        //刷新显示
        public void RefreshDisplay()
        {
            if (modelPartData == null || spriteRenderer == null) return;
            var currentStyle = modelPartData.GetCurrentStyle();
            if (string.IsNullOrEmpty(currentStyle)) return;

            if (modelPartData.renderMode == "sprite")
            {
                //静态Sprite模式：直接设置sprite
                var sprite = ResourceLoaderManager.Instance.GetSprite(currentStyle);
                if (sprite != null) spriteRenderer.sprite = sprite;
            }
            //动态Animation模式的刷新由动画系统自动处理
        }
    }
}