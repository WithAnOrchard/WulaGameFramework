using System.Collections.Generic;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.EventManager;
using EssSystem.EssManager.UIManager.Dao;

namespace EssSystem.EssManager.UIManager
{
    //管理器层 - 编排层，所有操作通过Event触发，禁止直接调用Service
    public class UIManager : ManagerBase
    {
        private bool _hasInit;

        public static UIManager Instance => InstanceWithInit<UIManager>(instance => { instance.Init(true); });

        public void Init(bool logMessage)
        {
            if (_hasInit) return;
            _hasInit = true;
        }

        //通过Event加载一个Canvas
        // public void LoadCanvas(string canvasId)
        // {
        //     EventManager.Instance.TriggerEvent("LoadCanvasEvent", new List<object> { canvasId });
        // }

        // //通过Event生成一个组件
        // public void GenerateComponent(string canvasId, string componentId, string uiType, string value,
        //     int posX, int posY, int sizeX, int sizeY)
        // {
        //     EventManager.Instance.TriggerEvent("GenerateComponentEvent", new List<object>
        //     {
        //         canvasId, componentId, uiType, value,
        //         posX.ToString(), posY.ToString(), sizeX.ToString(), sizeY.ToString()
        //     });
        // }


        //通过Event注册一个Canvas数据并加载
        public void RegisterAndLoadCanvas(UICanvas canvasData)
        {
            EventManager.Instance.TriggerEvent("RegisterCanvasEvent", new List<object> { canvasData });
        }

        //对外查询接口 - 通过DataSpaces获取Canvas运行时实体（纯读取，不走Event）
        public UICanvas GetCanvas(string id)
        {
            return UIService.Instance.GetCanvas(id);
        }
    }
}