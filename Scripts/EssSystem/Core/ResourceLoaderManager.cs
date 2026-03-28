using System.Collections.Generic;
using System.IO;
using EssSystem.Core.Dao;
using EssSystem.Core.Singleton;
using UnityEngine;
using FileMode = System.IO.FileMode;

namespace EssSystem.Core
{
    public class ResourceLoaderManager : SingletonMono<ResourceLoaderManager>
    {
        [SerializeField] public List<Sprite> SpriteList = new();

        public Dictionary<string, AudioClip> AudioClips = new();
        private Font defaultFont;
        public Dictionary<string, Font> fonts = new();
        public Dictionary<string, GameObject> Prefabs = new();
        public Dictionary<string, Sprite> Sprites = new();


        private void Awake()
        {
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static byte[] GetImageByte(string imagePath)
        {
            var files = new FileStream(imagePath, FileMode.Open);
            var imgByte = new byte[files.Length];
            files.Read(imgByte, 0, imgByte.Length);
            files.Close();
            return imgByte;
        }

        private Sprite TextureToSprite(Texture2D tex)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return sprite;
        }

        public AudioClip GetAudio(string audioName)
        {
            return AudioClips.GetValueOrDefault(audioName);
        }

        public Font GetFont(string fontName)
        {
            if (fonts.GetValueOrDefault(fontName) == null) return defaultFont;
            return fonts.GetValueOrDefault(fontName);
        }

        public void LoadFonts()
        {
            var font = Resources.LoadAll<Font>("Fonts");
            foreach (var f in font) fonts.TryAdd(f.name, f);
        }

        public void LoadPrefabs()
        {
            var gameObjects = Resources.LoadAll<GameObject>("Prefabs");
            foreach (var gameObject in gameObjects) Prefabs.TryAdd(gameObject.name, gameObject);
        }

        public void LoadSprites()
        {
            var sprites = Resources.LoadAll<Sprite>("Sprites");

            LogMessage("加载内部素材");
            foreach (var sprite in sprites)
            {
                LogMessage("正在加载素材" + sprite.name);
                Sprites.TryAdd(sprite.name, sprite);
            }
        }


        public void LoadAudioClips()
        {
            var audios = Resources.LoadAll<AudioClip>("AduioClips");
            foreach (var audioClip in audios) AudioClips.TryAdd(audioClip.name, audioClip);
            foreach (var audioClip in audios) Debug.Log(audioClip.name);
        }


        public void LoadExternalSprites(string externalSpritesPath)
        {
            LogMessage("读取外部素材" + externalSpritesPath);
            if (Directory.Exists(externalSpritesPath))
            {
                var directoryInfo = new DirectoryInfo(externalSpritesPath);
                foreach (var directoryInner in directoryInfo.GetDirectories())
                    LoadExternalSprites(directoryInner.FullName);

                foreach (var file in directoryInfo.GetFiles())
                    if (file.Name.EndsWith(".png"))
                    {
                        var tx = new Texture2D(16, 16);
                        tx.LoadImage(GetImageByte(file.FullName));
                        Sprites.TryAdd(file.Name.Replace(".png", ""), TextureToSprite(tx));
                        LogMessage("读取素材" + file.Name);
                        SpriteList.Add(TextureToSprite(tx));
                    }

                return;
            }

            LogMessage(externalSpritesPath + "素材目录路径不存在!");
            DataService.Instance.CreateDataFolder(externalSpritesPath);
        }

        public Sprite GetSprite(string name)
        {
            return Sprites.GetValueOrDefault(name);
        }

        public GameObject GetPrefab(string name)
        {
            return Prefabs.GetValueOrDefault(name);
        }


        public override void Init(bool logMessage = false)
        {
            if (!hasInit)
            {
                Debug.Log("初始化资源加载器");
                this.logMessage = logMessage;
                LoadPrefabs();
                LoadSprites();
                LoadAudioClips();
                LoadFonts();
                hasInit = true;
            }
        }
    }
}