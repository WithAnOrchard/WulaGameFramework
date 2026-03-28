using System.Collections.Generic;
using System.IO;
using CharacterManager.Dao;
using CharacterManager.Entity;
using EssSystem.Core;
using EssSystem.Core.AbstractClass;
using EssSystem.EssManager.CharacterManager.Entity;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace CharacterManager
{
    public class CharacterService : ServiceBase
    {
        //所有角色模型数据
        public List<CharacterModel> characterModels = new();

        //当前活跃的模型实体
        public ModelGameObject currentModelGameObject;

        //当前查看的模型下标
        public int currentModelIndex;

        public float frame = 5;
        public float frameRate = 30;

        public static CharacterService Instance => InstanceWithInit<CharacterService>(instance =>
        {
            if (!instance.DataSpaces.ContainsKey("Character"))
                instance.DataSpaces.Add("Character", new Dictionary<string, object>());
        });

        //获取模型数据
        public CharacterModel GetModel(string id)
        {
            foreach (var model in characterModels)
                if (id.Equals(model.id))
                    return model;

            return null;
        }

        //生成模型实体
        public ModelGameObject GenerateModel(CharacterModel characterModel)
        {
            if (characterModel == null) return null;

            var root = new GameObject();
            root.name = characterModel.id;
            var model = root.AddComponent<ModelGameObject>();

            foreach (var modelPart in characterModel.ModelParts)
            {
                var part = new GameObject();
                part.name = modelPart.id;
                part.transform.SetParent(model.transform);

                var partGO = part.AddComponent<ModelPartGameObject>();
                partGO.modelPartData = modelPart;
                model.modelParts.Add(partGO);

                //添加SpriteRenderer
                partGO.spriteRenderer = part.AddComponent<SpriteRenderer>();
                partGO.spriteRenderer.sortingLayerID = modelPart.layer;
                partGO.spriteRenderer.sortingOrder = modelPart.orderInLayer;

                if (modelPart.renderMode == "animation")
                {
                    //动态Animation模式
                    partGO.animator = part.AddComponent<Animator>();
                    SetupAnimator(characterModel, modelPart, partGO);
                }
                else
                {
                    //静态Sprite模式：显示当前样式
                    partGO.RefreshDisplay();
                }
            }

            DataSpaces["Character"][characterModel.id] = model;
            return model;
        }

        //设置动画（仅编辑器模式可用）
        private void SetupAnimator(CharacterModel characterModel, ModelPart modelPart, ModelPartGameObject partGO)
        {
#if UNITY_EDITOR
            var animatorController = new AnimatorController();
            animatorController.AddLayer("Base Layer");

            var currentStyle = modelPart.GetCurrentStyle();
            if (string.IsNullOrEmpty(currentStyle)) return;

            //加载sprite序列路径
            if (!string.IsNullOrEmpty(modelPart.spritesPath))
            {
                var fullPath = Application.dataPath + modelPart.spritesPath;
                if (Directory.Exists(fullPath))
                {
                    var directoryInfo = new DirectoryInfo(fullPath);
                    foreach (var file in directoryInfo.GetFiles())
                        if (file.Name.EndsWith(".png"))
                        {
                            var spriteName = file.Name.Replace(".png", "");
                            if (!modelPart.styles.Contains(spriteName)) modelPart.styles.Add(spriteName);
                        }
                }
            }

            //为每个动画状态生成AnimationClip
            var frameIndex = 0;
            foreach (var state in modelPart.ModelPartStates)
            {
                var spriteNames = new List<string>();
                for (var i = 0; i < state.span; i++) spriteNames.Add(currentStyle + "_" + (frameIndex + i));

                var clip = GenerateAnimationClip(spriteNames);
                clip.name = currentStyle + state.id;

                var animState = animatorController.layers[0].stateMachine.AddState(clip.name);
                animState.motion = clip;

                if (clip.name.Contains(modelPart.defaultAnimation))
                    animatorController.layers[0].stateMachine.defaultState = animState;

                frameIndex += state.span;
            }

            partGO.animator.runtimeAnimatorController = animatorController;
#endif
        }

        //生成AnimationClip
        public AnimationClip GenerateAnimationClip(List<string> sprites)
        {
            var clip = new AnimationClip();
#if UNITY_EDITOR
            var binding = new EditorCurveBinding();
            binding.type = typeof(SpriteRenderer);
            binding.path = "";
            binding.propertyName = "m_Sprite";

            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (var i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe();
                keyframes[i].time = i * frame / frameRate;
                var sprite = ResourceLoaderManager.Instance.GetSprite(sprites[i]);
                keyframes[i].value = sprite;
            }

            clip.frameRate = frameRate;
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
#endif
            return clip;
        }

        //切换模型
        public ModelGameObject SwitchModel(int modelIndex)
        {
            if (characterModels.Count == 0) return null;
            currentModelIndex = modelIndex % characterModels.Count;

            //销毁旧模型
            if (currentModelGameObject != null) Object.Destroy(currentModelGameObject.gameObject);

            //生成新模型
            var model = characterModels[currentModelIndex];
            currentModelGameObject = GenerateModel(model);
            currentModelGameObject.transform.localScale = new Vector3(10, 10, 10);

            return currentModelGameObject;
        }

        //切换下一个模型
        public ModelGameObject NextModel()
        {
            return SwitchModel(currentModelIndex + 1);
        }

        //切换上一个模型
        public ModelGameObject PrevModel()
        {
            return SwitchModel(currentModelIndex - 1 + characterModels.Count);
        }

        //切换指定部位的样式
        public void SwitchPartStyle(int partIndex, bool next)
        {
            if (currentModelGameObject == null) return;
            if (partIndex < 0 || partIndex >= currentModelGameObject.modelParts.Count) return;

            var partGO = currentModelGameObject.modelParts[partIndex];
            if (next)
                partGO.SwitchNextStyle();
            else
                partGO.SwitchPrevStyle();
        }

        //获取当前模型的部位数量
        public int GetCurrentPartCount()
        {
            if (currentModelIndex < 0 || currentModelIndex >= characterModels.Count) return 0;
            return characterModels[currentModelIndex].ModelParts.Count;
        }

        //获取当前模型数据
        public CharacterModel GetCurrentModel()
        {
            if (currentModelIndex < 0 || currentModelIndex >= characterModels.Count) return null;
            return characterModels[currentModelIndex];
        }

        //初始化
        public void InitAll()
        {
            ResourceLoaderManager.Instance.Init();
        }
    }
}