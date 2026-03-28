using System.Collections.Generic;
using CharacterManager.Dao;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.EventManager;
using EssSystem.EssManager.CharacterManager.Entity;

namespace CharacterManager
{
    //管理器层 - 编排层，所有操作通过Event触发，禁止直接调用Service
    public class CharacterManager : ManagerBase
    {
        private bool _hasInit;

        public static CharacterManager Instance => InstanceWithInit<CharacterManager>(instance =>
        {
            instance.Init(true);
        });

        //对外查询接口 - 获取模型数据列表
        public List<CharacterModel> characterModels => CharacterService.Instance.characterModels;

        //对外查询接口 - 获取/设置当前模型实体
        public ModelGameObject currentModelGameObject
        {
            get => CharacterService.Instance.currentModelGameObject;
            set => CharacterService.Instance.currentModelGameObject = value;
        }

        public void Init(bool logMessage)
        {
            if (_hasInit) return;
            _hasInit = true;
            EventManager.Instance.TriggerEvent("InitCharacterEvent");
        }

        //对外查询接口 - 获取模型数据
        public CharacterModel GetModel(string id)
        {
            return CharacterService.Instance.GetModel(id);
        }

        //对外查询接口 - 获取当前模型
        public CharacterModel GetCurrentModel()
        {
            return CharacterService.Instance.GetCurrentModel();
        }

        //对外查询接口 - 获取当前模型部位数量
        public int GetCurrentPartCount()
        {
            return CharacterService.Instance.GetCurrentPartCount();
        }

        //对外接口 - 生成模型
        public ModelGameObject GenerateModel(CharacterModel characterModel)
        {
            return CharacterService.Instance.GenerateModel(characterModel);
        }

        //通过Event切换模型
        public void SwitchModel(string direction)
        {
            EventManager.Instance.TriggerEvent("SwitchModelEvent", new List<object> { direction });
        }

        //通过Event切换部位样式
        public void SwitchPartStyle(int partIndex, string direction)
        {
            EventManager.Instance.TriggerEvent("SwitchPartStyleEvent", new List<object>
            {
                partIndex.ToString(), direction
            });
        }
    }
}