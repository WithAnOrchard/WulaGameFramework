using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using EssSystem.Core.Singleton;
using UnityEngine;

namespace EssSystem.Core.Dao
{
    //数据存取 及获取方式
    public class DataService : Singleton<DataService>
    {
        //对应的是存储空间 所有的dao层数据存储应在这里存取 并且由此层定时存档？是否需要存档呢。
        
        
       
        
        public void SaveJson<T>(T data, string filepath)
        {
            Log("保存数据" + filepath);
            if (!File.Exists(filepath))
            {
                FileInfo file = new FileInfo(filepath);

                if (!file.Directory.Exists)
                {
                    Log(file.Directory.FullName + "不存在，创建中");
                    file.Directory.Create();
                }

                File.Create(filepath).Close();
            }

            string json = JsonUtility.ToJson(data, true);
            using (StreamWriter sw = new StreamWriter(filepath))
            {
                sw.WriteLine(json);
                sw.Close();
                sw.Dispose();
            }
        }
        
        public List<T> LoadAllJson<T>(string folderPath, List<T> inputList) where T : new()
        {
            Log("读取外部文件夹内所有文件" + folderPath);
            CreateDataFolder(folderPath);

            if (inputList == null)
            {
                inputList = new List<T>();
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);
            foreach (var directoryInner in directoryInfo.GetDirectories())
            {
                inputList.AddRange(LoadAllJson<T>(directoryInner.FullName, inputList));
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                if (file.Name.EndsWith(".json"))
                {
                    inputList.Add(LoadJson<T>(file.FullName));
                }
            }

            return inputList;
        }

        public T LoadJson<T>(string filepath) where T : new()
        {
            string json = "";

            if (!File.Exists(filepath))
            {
                Log("数据文件缺失，创建新的文件");
                T target = new T();
                SaveJson(target, filepath);
            }

            using (StreamReader sr = new StreamReader(filepath))
            {
                json = sr.ReadToEnd();
                sr.Close();
            }

            return JsonUtility.FromJson<T>(json);
        }
        
        public void CreateDataFolder(string folderName)
        {
            if (!Directory.Exists(folderName))
                Directory.CreateDirectory(folderName);
        }
        
        public T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;
                return (T)formatter.Deserialize(ms);
            }
        }

        protected override void Init(Boolean logMessages = true)
        {
            this.LogMessage = logMessages;
        }
    }
}