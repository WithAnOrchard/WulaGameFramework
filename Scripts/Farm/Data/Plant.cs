using System;
using System.Numerics;

namespace Farm.Data
{
    [Serializable]
    public class Plant
    {
        public int maxAge;

        public long currentAge;


        //生长速度
        public long growTime;

        //生成时间
        public long spawnTime;


        public long growSpeed;

        //多久长一个阶段
        public long growSpan;


        public string name;

        public string currentSprite;

        public string[] sprites;

        public int scale;

        public float offSetX;
        public float offSetY;
        public float offSetZ;
        public string layer;
        public int orderInLayer;

        public Plant()
        {
            this.maxAge = 0;
            this.currentAge = 0;
            this.currentSprite = "currentSprite";
            this.growTime = 0;
            this.spawnTime = 0;
            this.growSpan = 0;
            this.name = "name";
            this.sprites = new string[5];
            this.scale = 1;
            this.offSetX = 0;
            this.offSetY = 0;
            this.offSetZ = 0;
            this.layer = "layer";
            this.orderInLayer = 0;
        }


        public Plant(int maxAge, long currentAge, string currentSprite, int growTime, long spawnTime, long growSpan,
            string name, string[] sprites, int scale, float offSetX, float offSetY, float offSetZ, string layer,
            int orderInLayer)
        {
            this.maxAge = maxAge;
            this.currentAge = currentAge;
            this.currentSprite = currentSprite;
            this.growTime = growTime;
            this.spawnTime = spawnTime;
            this.growSpan = growSpan;
            this.name = name;
            this.sprites = sprites;
            this.scale = scale;
            this.offSetX = offSetX;
            this.offSetY = offSetY;
            this.offSetZ = offSetZ;
            this.layer = layer;
            this.orderInLayer = orderInLayer;
        }
    }
}