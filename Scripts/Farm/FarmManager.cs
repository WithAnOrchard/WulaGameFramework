using System;
using System.Collections.Generic;
using EssSystem;
using EssSystem.Core;
using EssSystem.Core.Dao;
using EssSystem.Core.EventManager;
using EssSystem.Core.Singleton;
using Farm.Data;
using Farm.GameObjects;
using Unity.VisualScripting;
using UnityEngine;

namespace Farm
{
    public class FarmManager : SingletonMono<FarmManager>
    {
        [SerializeField] public Data.Farm farm;

        //所有的植物样板数据
        [SerializeField] public List<Plant> plants;

        public FarmGameObject farmGameObject;
        
        
        
        

        public FarmGameObject GenerateFarm()
        {
            LogMessage("初始化农场");
            FarmGameObject farmobject = GenerateFarmGameObject();
            farmGameObject = farmobject;

            foreach (var land in farm.FarmLands)
            {
                //创建FarmLand实体并添加到FarmLandGameobject便于管理
                FarmLandGameObject farmland = CreateFarmLand();
                //将其绑定到farm
                farmobject.FarmLandGameObjects.Add(farmland);

                //创建FarmLand上的植物实体 并将其添加到FarmLand
                PlantGameObject plant = GeneratePlantGameObject(land.plant, farmland);
            }

            int index = 0;
            foreach (var farmlandbject in farmobject.FarmLandGameObjects)
            {
                farmlandbject.transform.Translate(farm.screenWidth / farm.maxFarmLands * index, 0, 0);
            }

            return farmobject;
        }

        public FarmGameObject GenerateFarmGameObject()
        {
            GameObject o = new GameObject();
            o.name = "Farm";
            o.transform.SetParent(transform);
            o.transform.Translate(new Vector3(farm.FarmLandConfig.offSetX, farm.FarmLandConfig.offSetY));
            FarmGameObject farmobject = o.AddComponent<FarmGameObject>();
            farmobject.FarmLandGameObjects = new List<FarmLandGameObject>();
            farmobject.AddComponent<SpriteRenderer>();
            return farmobject;
        }

        public FarmLandGameObject CreateFarmLand()
        {
            GameObject farmLand = new GameObject();
            farmLand.name = "FarmLand";

            FarmLandGameObject farmLandGameObject = farmLand.AddComponent<FarmLandGameObject>();
            farmLandGameObject.spriteRenderer = farmLandGameObject.AddComponent<SpriteRenderer>();
            //载入farmland配置
            farmLandGameObject.spriteRenderer.sprite =
                ResourceLoaderManager.Instance.GetSprite(farm.FarmLandConfig.farmLandSprite);
            farmLandGameObject.transform.localScale =
                new Vector3(farm.FarmLandConfig.sacle, farm.FarmLandConfig.sacle);
            farmLandGameObject.spriteRenderer.sortingLayerName = farm.FarmLandConfig.layer;
            farmLandGameObject.spriteRenderer.sortingOrder = farm.FarmLandConfig.orderInLayer;
            farmLand.transform.SetParent(farmGameObject.transform);
            farmLand.transform.Translate(farm.FarmLandConfig.offSetX, farm.FarmLandConfig.offSetY, 0);
            return farmLandGameObject;
        }


        public PlantGameObject GeneratePlantGameObject(Plant plantData, FarmLandGameObject farmLand)
        {
            GameObject plant = new GameObject();
            plant.name = plantData.name;
            plant.transform.SetParent(farmLand.transform, false);
            plant.transform.localPosition = new Vector3(plantData.offSetX, plantData.offSetY);
            plant.transform.localScale = new Vector3(plantData.scale, plantData.scale);

            PlantGameObject plantGameObject = plant.AddComponent<PlantGameObject>();
            plantGameObject.spriteRenderer = plantGameObject.AddComponent<SpriteRenderer>();

            farmLand.plantGameObject = plantGameObject;

            plantGameObject.spriteRenderer.sortingLayerName = plantData.layer;
            plantGameObject.spriteRenderer.sortingOrder = plantData.orderInLayer;


            plantGameObject.spriteRenderer.sprite = ResourceLoaderManager.Instance.GetSprite(plantData.sprites[0]);
            return plantGameObject;
        }

        public void LoadFarmData()
        {
            LogMessage("读取农场数据");
            farm = DataService.Instance.LoadJson<Data.Farm>("Data/FarmData/Farm.json");
        }

        public void LoadFarmSprites()
        {
            LogMessage("读取农场外部材质");
            ResourceLoaderManager.Instance.LoadExternalSprites("Data/FarmData/Sprites");
        }

        public void LoadPlantsData()
        {
            LogMessage("读取植物样版数据文件");
            plants = DataService.Instance.LoadAllJson<Plant>("Data/FarmData/Plants", null);
            foreach (var plant in plants)
            {
                // LogMessage(JsonUtility.ToJson(plant, true));
            }

            DataService.Instance.SaveJson(
                new Plant(),
                "Data/FarmData/Plants/example.json");
        }


        public void AddNewCrate(object o)
        {
            //如果没有空的花盆 但是
            if (farm.FarmLands.Count < farm.maxFarmLands)
            {
                LogMessage("新增花盆成功");
                FarmLandGameObject farmland = CreateFarmLand();
                farmGameObject.FarmLandGameObjects.Add(farmland);
                //生成物品并且克隆生成新数据
                farmland.transform.Translate(farm.screenWidth / farm.maxFarmLands * farm.FarmLands.Count, 0, 0);
                farm.FarmLands.Add(new FarmLand(farm.FarmLands.Count, null, 100, 100));
                return;
            }

            LogMessage("新增花盆失败");
        }

        public Boolean PlantNewPlant(string plantName)
        {
            foreach (var plant in plants)
            {
                if (plant.name == plantName)
                {
                    foreach (var farmLand in farm.FarmLands)
                    {
                        //如果有空的花盆则种植下去
                        if (farmLand.plant == null || farmLand.plant.name == "name")
                        {
                            LogMessage("新增植物" + plantName + "成功");
                            FarmLandGameObject farmLandGameObject = farmGameObject.FarmLandGameObjects[farmLand.index];
                            PlantGameObject plantGameObject = GeneratePlantGameObject(plant, farmLandGameObject);
                            farmLand.plant = DataService.Instance.DeepClone(plant);
                            farmLand.plant.spawnTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds();
                            return true;
                        }
                    }
                }
            }

            LogMessage("新增植物" + plantName + "失败");
            return false;
        }

        public Boolean HarvestPlant(int index)
        {
            if (index >= farm.FarmLands.Count)
            {
                LogMessage("收获时候使用了错误的下标");
                return false;
            }

            if (farm.FarmLands[index].plant.name == "name")
            {
                LogMessage("收获花盆植物为空");
                return false;
            }
            else
            {
                if (farm.FarmLands[index].plant.currentAge != farm.FarmLands[index].plant.maxAge)
                {
                    LogMessage("植物未成熟");
                    return false;
                }

                DestroyImmediate(farmGameObject.FarmLandGameObjects[index].plantGameObject.gameObject);
                Debug.Log("收获" + farm.FarmLands[index].plant.name + "成功");
                farm.FarmLands[index].plant = null;
                return true;
            }
        }

        public void RegisterFarmEvents()
        {
            EventManager.Instance.Subscribe("新增花盆", AddNewCrate);

            EventManager.Instance.Subscribe<String>("新增植物", plantName => { PlantNewPlant(plantName); });
            EventManager.Instance.Subscribe<int>("收获植物", index => { HarvestPlant(index); });
            //更新植物事件
            EventManager.Instance.Subscribe<int>("农场更新数据", UpdateFarm);
        }


        public void UpdateFarm(int time)
        {
            int index = 0;
            foreach (var farmLand in farm.FarmLands)
            {
                //降低水分植
                if (farmLand.currentWet - time >= 0)
                {
                    farmLand.currentWet -= time;
                }
                else
                {
                    farmLand.currentWet = 0;
                }

                if (farmLand.plant.name != "name")
                {
                    Plant p = farmLand.plant;
                    //对植物进行生长
                    p.growTime += time * p.growSpeed;

                    if (p.growTime >= p.growSpan)
                    {
                        long cureentAge = p.growTime / p.growSpan;
                        if (cureentAge <= p.maxAge)
                        {
                            p.currentAge = cureentAge;
                            p.currentSprite = p.sprites[p.currentAge - 1];
                            //更新植物实体
                            farmGameObject.FarmLandGameObjects[index].plantGameObject.spriteRenderer.sprite =
                                ResourceLoaderManager.Instance.GetSprite(p.currentSprite);
                        }
                    }
                    //进行更新物品
                }

                index++;
            }
        }


        public override void Init(Boolean logMessage = false)
        {
            this.logMessage = logMessage;
            LoadFarmSprites();
            LoadPlantsData();
            LoadFarmData();
            farmGameObject = GenerateFarm();
            RegisterFarmEvents();
        }
    }
}