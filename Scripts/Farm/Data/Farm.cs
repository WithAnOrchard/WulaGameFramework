using System.Collections.Generic;
using System.Numerics;

namespace Farm.Data
{
    [System.Serializable]
    public class Farm
    {
        public List<FarmLand> FarmLands;

        public FarmLandConfig FarmLandConfig;

        public int maxFarmLands = 5;

        public float screenWidth;

        public Farm()
        {
            FarmLands = new List<FarmLand>();
        }


        public void Plant()
        {
        }

        public void updateFarmLands()
        {
        }
    }

    [System.Serializable]
    public class FarmLandConfig
    {
        public string farmLandSprite;

        public int sacle;

        public string layer;

        public int orderInLayer;

        public float offSetX;
        public float offSetY;
        public float offSetZ;
    }
}