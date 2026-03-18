using System;
using System.Collections.Generic;
using System.IO;
using EssSystem;
using EssSystem.Core;
using EssSystem.Core.Singleton;
using EssSystem.UIManager;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace CharacterManager
{
    public class CharacterManager : SingletonMono<CharacterManager>
    {
        //几帧一张图
        public float frame = 5;
        public float frameRate = 30;


        //纯数据化内容
        public List<CharacterModel> characterModels = new List<CharacterModel>();

        //缓存数据生成后的内容
        public List<AnimationsCache> AnimationsCaches = new List<AnimationsCache>();


        private void Awake()
        {
            Instance = this;
            Init();
        }

        public override void Init(bool logMessage = true)
        {
            ResourceLoaderManager.Instance.Init();
            GenerateAnimations();
            CharacterViewer.Instance.Init(true);
        }


        void Start()
        {

        }

        public CharacterModel GetModel(string id)
        {
            foreach (var model in characterModels)
            {
                if (id.Equals(model.id))
                {
                    return model;
                }   
            }
            return null;
        }

        public ModelGameObject GenerateModel(CharacterModel characterModel)
        {
            if(characterModel==null)return null;
            GameObject root=new GameObject();
            root.name = characterModel.id;
            ModelGameObject model = root.AddComponent<ModelGameObject>();
            model.modelParts = new List<ModelPartGameObject>();
            //生成部位
            foreach (var modelPart in characterModel.ModelParts)
            {
                //生成部分实体
                GameObject part = new GameObject();
                part.name = modelPart.id;
                part.transform.SetParent(model.transform);
                ModelPartGameObject modelPartGameObject= part.AddComponent<ModelPartGameObject>();
                model.modelParts.Add(modelPartGameObject);
                
                //设置渲染器及渲染层级
                SpriteRenderer spriteRenderer= modelPartGameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingLayerID = modelPart.layer;
                spriteRenderer.sortingOrder = modelPart.orderInLayer;
                
                //设置动画机及动画
                modelPartGameObject.animator= modelPartGameObject.AddComponent<Animator>();
                AnimatorController animatorController = new AnimatorController();
                
                animatorController.AddLayer("Base Layer");
                List<AnimationClip> clips= GetRandomAnimation(characterModel.id, modelPart.id);
                AnimatorState[] states = new AnimatorState[clips.Count] ;
                int index = 0;
                foreach (var animationClip in clips)
                {
                    states[index]= animatorController.layers[0].stateMachine.AddState(animationClip.name);
                    states[index].motion = animationClip;
                    
                    //设置默认动画
                    if (animationClip.name.Contains(modelPart.defaultAnimation))
                    {
                        animatorController.layers[0].stateMachine.defaultState = states[index];
                    }
                    index++;
                }
                
                modelPartGameObject.animator.runtimeAnimatorController = animatorController;

            }
            
            return model;
        }
        
        
        /// /////////////////////////////////////////////////////////////////////////
        /// 以下为动画生成部分
        
        public List<AnimationClip> GetRandomAnimation(string modelName, string partName)
        {
           Random random= new Random();
            foreach (var animationsCache in AnimationsCaches)
            {
                
                if (animationsCache.modelName .Equals(modelName) && animationsCache.modelPartName.Equals(partName))
                {
                    return animationsCache.StyleToClips[random.Next(animationsCache.StyleToClips.Count)].animations ;
                }
            }

            return null;
        }
        
        public void GenerateAnimations()
        {
            foreach (var characterModel in characterModels)
            {
                string modelName = characterModel.id;
                //生成部位
                foreach (var modelPart in characterModel.ModelParts)
                {
                    AnimationsCache animationsCache=   new AnimationsCache();
                    animationsCache.modelName = modelName;
                    animationsCache.modelPartName = modelPart.id;
                    //生成动画
                    //遍历所有状态
                    int index = 0;
                    foreach (var modelPartState in  modelPart.ModelPartStates)
                    {
                        if (!modelPart.usingSpritesPath.Equals(""))
                        {
                            //读取某个文件夹下所有的png
                            DirectoryInfo directoryInfo = new DirectoryInfo(Application.dataPath+modelPart.usingSpritesPath);
                            foreach (var file in directoryInfo.GetFiles())
                            {
                                if (file.Name.EndsWith(".png"))
                                {
                                   modelPart.usingSprite.Add(file.Name.Replace(".png",""));
                                }
                            }
                        }
                        //遍历所有样式
                        foreach (var sprite in modelPart.usingSprite)
                        {
                            StyleToClip styleToClip= animationsCache.getStyleToClip(sprite);
                            
                            List<string> animation = new List<string>();
                            for (int i = 0; i <modelPartState.span; i++)
                            {
                                animation.Add(sprite+"_"+ (index+i));
                            }
                            AnimationClip clip = GenerateAnimationClip(animation);
                            clip.name = sprite+modelPartState.id;
                            
                            styleToClip.animations.Add(clip);

                            bool hasThisStyle = false; 
                            foreach (var inner in animationsCache.StyleToClips)
                            {
                                if (inner.style.Equals(sprite))
                                {
                                    hasThisStyle = true;
                                }
                            }

                            if (!hasThisStyle)
                            {
                                animationsCache.StyleToClips.Add(styleToClip);
                            }
                        }
                        index+=modelPartState.span;
                        
                    }
                    
                    AnimationsCaches.Add(animationsCache);
                }
            }
        }

        public AnimationClip GenerateAnimationClip(List<string> sprites)
        {
            AnimationClip clip = new AnimationClip();
            EditorCurveBinding binding = new EditorCurveBinding() ;
            binding.type = typeof(SpriteRenderer);
            binding.path = "";
            binding.propertyName = "m_Sprite";
            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe();
                keyframes[i].time = i *frame/ frameRate;
                Sprite sprite=ResourceLoaderManager.Instance.GetSprite(sprites[i]);
                keyframes[i].value = sprite;
            }
            clip.frameRate = frameRate;
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            
            return clip;
        }


        /// /////////////////////////////////////////////////////////////////////////
        /// 以下为缓存数据用实体
        public class ModelGameObject : MonoBehaviour
        {
           public List<ModelPartGameObject> modelParts;
        }

        public class ModelPartGameObject : MonoBehaviour
        {
            public Animator animator;
            
            public List<AnimationClip> animations;
        }
        
        
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// 缓存用内容
        [System.Serializable]
        public class AnimationsCache
        {
            //模型名称
            public string modelName;
            //模型部分名称
            public string modelPartName;

            public List<StyleToClip> StyleToClips=new List<StyleToClip>();

            public StyleToClip getStyleToClip(string style)
            {
                foreach (var styleToClip in StyleToClips)
                {
                    if (styleToClip.style.Equals(style))
                    {
                        return styleToClip;
                    }
                }

                return new StyleToClip(style);
            }



        }
        [System.Serializable]
        public class StyleToClip
        {
            public string style;
            public List<AnimationClip> animations=new List<AnimationClip>();

            public StyleToClip(string name)
            {
                this.style = name;
            }
        }
        
        
        
        
        
        
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// 数据化内容
        [System.Serializable]
        public class CharacterModel
        {
            public string id;
            public List<ModelPart> ModelParts = new List<ModelPart>();
        }

        [System.Serializable]
        public class ModelPart
        {
            public string id;
            //所有可用素材
            public List<string> usingSprite;
            //直接加载路径？
            public string usingSpritesPath;

            //每个部位有多少种状态(既一个png需要分割成多少个动画)
            public List<ModelPartState> ModelPartStates;
            public int layer;
            public int orderInLayer;

            public string defaultAnimation = "静止";

        }
        
        [System.Serializable]
        public class ModelPartState
        {
            
            public string id;
            //这一批素材中的多少到多少为这个动画
            public int span;
        }

      
    }
}
