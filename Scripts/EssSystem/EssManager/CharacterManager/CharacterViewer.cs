using System;
using System.Collections.Generic;
using EssSystem;
using EssSystem.UIManager;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Unity.VisualScripting;
using System.Diagnostics;
using EssSystem.Core.Singleton;
using Debug = UnityEngine.Debug; // 添加JSON解析库，如果未安装请安装Newtonsoft.Json

namespace CharacterManager
{
    public class CharacterViewer : SingletonMono<CharacterViewer>
    {
        //用以控制显示所有可控项 比如用按钮调整头盔等？
        public UICanvas canvas;

        
        public GameObject characterModel;

        //展示中的模型下标
        public int index=0;
        


        private void Awake()
        {
            Instance = this;

        }

        public void Start()
        {
        }


        public void GenerateModelGameObject(string model)
        {
            Destroy(characterModel);
            CharacterManager.ModelGameObject modelGameObject=   CharacterManager.Instance.GenerateModel(CharacterManager.Instance.GetModel(model));
            modelGameObject.transform.localScale=new Vector3(10,10,10);
            characterModel = modelGameObject.gameObject;
            UIManager.Instance.GetCanvasGameObject("CharacterViewer").GetComponent("模型选择").gameObject.GetComponent<Text>().text = model;
        }

        public override void Init(bool logMessage)
        {  
            UIManager.Instance.Init(true);
            InitCanvas(); 
            GenerateModelGameObject(CharacterManager.Instance.characterModels[index].id);

        }

        public void InitCanvas()
        {
           canvas = JsonConvert.DeserializeObject<UICanvas>(defaultCanvasJson);
           UIManager.Instance.canvases.Add(canvas);
           UIManager.Instance.LoadCanvas(canvas);
           foreach (var canvase in  UIManager.Instance.canvases)
           {
               Debug.Log(canvase.id);
           }
         
           Button leftButton=  UIManager.Instance.GetCanvasGameObject("CharacterViewer").GetComponent("模型选择左按钮").gameObject.GetComponent<Button>();
           leftButton.onClick.AddListener(() =>
           {
               if (index >= 1)
               {
                   index--;
                   GenerateModelGameObject(CharacterManager.Instance.characterModels[index].id);
                   GenerateAllPart(CharacterManager.Instance.characterModels[index]);
               }
           });
           Button rightButton=  UIManager.Instance.GetCanvasGameObject("CharacterViewer").GetComponent("模型选择左按钮").gameObject.GetComponent<Button>();
           rightButton.onClick.AddListener(() =>
           {
               if (index + 1 < CharacterManager.Instance.characterModels.Count)
               {
                   index++;
                   GenerateModelGameObject(CharacterManager.Instance.characterModels[index].id);
                   GenerateAllPart(CharacterManager.Instance.characterModels[index]);
               }
           });
          
        }




        public void GenerateAllPart(CharacterManager.CharacterModel characterModel)
        {
            foreach (var modelModelPart in characterModel.ModelParts)
            {
                // modelModelPart.id
            }
        }
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        private static readonly string defaultCanvasJson = @"{
    ""id"": ""CharacterViewer"",
    ""position"": {
        ""x"": 0,
        ""y"": 0
    },
    ""size"": {
        ""x"": 1920,
        ""y"": 1080
    },
    ""components"": [
        {
            ""id"": ""标题"",
            ""uiType"": 2,
            ""position"": {
                ""x"": 0,
                ""y"": 480
            },
            ""size"": {
                ""x"": 400,
                ""y"": 50
            },
            ""value"": """"
        },
        {
            ""id"": ""模型选择"",
            ""uiType"": 2,
            ""position"": {
                ""x"": 0,
                ""y"": 480
            },
            ""size"": {
                ""x"": 500,
                ""y"": 100
            },
            ""value"": ""Minecraft""
        },
        {
            ""id"": ""模型选择左按钮"",
            ""uiType"": 0,
            ""position"": {
                ""x"": -300,
                ""y"": 480
            },
            ""size"": {
                ""x"": 60,
                ""y"": 60
            },
            ""value"": ""GUI_5""
        },
        {
            ""id"": ""模型选择右按钮"",
            ""uiType"": 0,
            ""position"": {
                ""x"": 300,
                ""y"": 480
            },
            ""size"": {
                ""x"": 60,
                ""y"": 60
            },
            ""value"": ""GUI_93""
        }
    ]
}";
    }
}