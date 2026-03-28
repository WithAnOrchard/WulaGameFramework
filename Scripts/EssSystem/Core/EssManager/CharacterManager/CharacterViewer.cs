using System.Collections.Generic;
using EssSystem.Core.Singleton;
using EssSystem.EssManager.UIManager;
using EssSystem.EssManager.UIManager.Dao;
using UnityEngine;

namespace CharacterManager
{
    public class CharacterViewer : SingletonMono<CharacterViewer>
    {
        //当前显示的模型实体
        public GameObject characterModelObj;

        //部位样式按钮列表（动态生成）
        private List<GameObject> partButtons = new();

        private void Awake()
        {
            Instance = this;
        }

        public override void Init(bool logMessage = true)
        {
            //确保UIManager已初始化
            UIManager.Instance.Init(true);

            //注册并加载CharacterViewer的Canvas
            var canvas = CreateViewerCanvas();
            UIManager.Instance.RegisterAndLoadCanvas(canvas);

            // //绑定模型切换按钮
            // BindModelSwitchButtons();
            //
            // //显示第一个模型
            // if (CharacterManager.Instance.characterModels.Count > 0)
            // {
            //     SwitchToCurrentModel();
            // }
        }

        //创建CharacterViewer的Canvas数据
        private UICanvas CreateViewerCanvas()
        {
            var canvas = new UICanvas();
            canvas.id = "CharacterViewer";
            canvas.position = new Vector2Int(0, 0);
            canvas.size = new Vector2Int(1920, 1080);

            //标题文本
            var title = new UIComponent();
            title.id = "标题";
            title.uiType = UIType.Text;
            title.position = new Vector2Int(0, 500);
            title.size = new Vector2Int(400, 50);
            title.value = "";
            canvas.components.Add(title.id, title);

            //模型名称显示
            var modelName = new UIComponent();
            modelName.id = "模型选择";
            modelName.uiType = UIType.Text;
            modelName.position = new Vector2Int(0, 480);
            modelName.size = new Vector2Int(500, 100);
            modelName.value = "";
            canvas.components.Add(modelName.id, modelName);

            //左切换按钮
            var leftBtn = new UIComponent();
            leftBtn.id = "模型选择左按钮";
            leftBtn.uiType = UIType.Button;
            leftBtn.position = new Vector2Int(-300, 480);
            leftBtn.size = new Vector2Int(60, 60);
            leftBtn.value = "GUI_5";
            canvas.components.Add(leftBtn.id, leftBtn);

            //右切换按钮
            var rightBtn = new UIComponent();
            rightBtn.id = "模型选择右按钮";
            rightBtn.uiType = UIType.Button;
            rightBtn.position = new Vector2Int(300, 480);
            rightBtn.size = new Vector2Int(60, 60);
            rightBtn.value = "GUI_93";
            canvas.components.Add(rightBtn.id, rightBtn);

            return canvas;
        }

        // //绑定模型切换按钮事件
        // private void BindModelSwitchButtons()
        // {
        //     var canvasGO = UIManager.Instance.GetCanvas("CharacterViewer");
        //     if (canvasGO == null) return;
        //
        //     //左按钮 - 上一个模型
        //     var leftBtnGO = canvasGO.GetComponent("模型选择左按钮");
        //     if (leftBtnGO != null)
        //     {
        //         Button leftBtn = leftBtnGO.gameObject.GetComponent<Button>();
        //         leftBtn.onClick.AddListener(() =>
        //         {
        //             CharacterManager.Instance.SwitchModel("prev");
        //             SwitchToCurrentModel();
        //         });
        //     }
        //
        //     //右按钮 - 下一个模型
        //     var rightBtnGO = canvasGO.GetComponent("模型选择右按钮");
        //     if (rightBtnGO != null)
        //     {
        //         Button rightBtn = rightBtnGO.gameObject.GetComponent<Button>();
        //         rightBtn.onClick.AddListener(() =>
        //         {
        //             CharacterManager.Instance.SwitchModel("next");
        //             SwitchToCurrentModel();
        //         });
        //     }
        // }
        //
        // //切换到当前模型并更新UI
        // private void SwitchToCurrentModel()
        // {
        //     //销毁旧模型显示
        //     if (characterModelObj != null)
        //     {
        //         Destroy(characterModelObj);
        //     }
        //
        //     //获取当前模型
        //     CharacterModel currentModel = CharacterManager.Instance.GetCurrentModel();
        //     if (currentModel == null) return;
        //
        //     //生成新模型
        //     var modelGO = CharacterManager.Instance.currentModelGameObject;
        //     if (modelGO == null)
        //     {
        //         modelGO = CharacterManager.Instance.GenerateModel(currentModel);
        //         CharacterManager.Instance.currentModelGameObject = modelGO;
        //     }
        //     characterModelObj = modelGO.gameObject;
        //
        //     //更新模型名称显示
        //     var canvasGO = UIManager.Instance.GetCanvasGameObject("CharacterViewer");
        //     if (canvasGO != null)
        //     {
        //         var nameDisplay = canvasGO.GetComponent("模型选择");
        //         if (nameDisplay != null)
        //         {
        //             Text text = nameDisplay.gameObject.GetComponent<Text>();
        //             if (text != null)
        //             {
        //                 text.text = currentModel.id;
        //             }
        //         }
        //     }
        //
        //     //重新生成部位样式按钮
        //     GeneratePartButtons(currentModel);
        // }
        //
        // //根据模型的部位数量动态生成样式切换按钮
        // private void GeneratePartButtons(CharacterModel model)
        // {
        //     //清除旧按钮
        //     foreach (var btn in partButtons)
        //     {
        //         Destroy(btn);
        //     }
        //     partButtons.Clear();
        //
        //     if (model == null) return;
        //
        //     var canvasGO = UIManager.Instance.GetCanvasGameObject("CharacterViewer");
        //     if (canvasGO == null) return;
        //
        //     //为每个部位生成一个切换按钮
        //     int startY = 350;
        //     int buttonHeight = 50;
        //     int spacing = 10;
        //
        //     for (int i = 0; i < model.ModelParts.Count; i++)
        //     {
        //         ModelPart part = model.ModelParts[i];
        //         int partIndex = i;
        //         int yPos = startY - i * (buttonHeight + spacing);
        //
        //         //创建部位按钮容器
        //         GameObject btnObj = new GameObject();
        //         btnObj.name = "PartButton_" + part.id;
        //         btnObj.transform.SetParent(canvasGO.transform);
        //
        //         //添加RectTransform
        //         RectTransform rect = btnObj.AddComponent<RectTransform>();
        //         rect.sizeDelta = new Vector2(200, buttonHeight);
        //         rect.anchoredPosition = new Vector2(600, yPos);
        //
        //         //添加按钮背景
        //         Image btnImage = btnObj.AddComponent<Image>();
        //         btnImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        //
        //         //添加按钮组件
        //         Button btn = btnObj.AddComponent<Button>();
        //         btn.onClick.AddListener(() =>
        //         {
        //             //点击切换该部位的下一个样式
        //             CharacterManager.Instance.SwitchPartStyle(partIndex, "next");
        //         });
        //
        //         //添加文本显示部位名称
        //         GameObject textObj = new GameObject();
        //         textObj.name = "Text";
        //         textObj.transform.SetParent(btnObj.transform);
        //         RectTransform textRect = textObj.AddComponent<RectTransform>();
        //         textRect.sizeDelta = new Vector2(200, buttonHeight);
        //         textRect.anchoredPosition = Vector2.zero;
        //
        //         Text text = textObj.AddComponent<Text>();
        //         text.text = part.id + " [" + part.GetCurrentStyle() + "]";
        //         text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        //         text.fontSize = 20;
        //         text.alignment = TextAnchor.MiddleCenter;
        //         text.color = Color.white;
        //
        //         partButtons.Add(btnObj);
        //     }
        // }
    }
}