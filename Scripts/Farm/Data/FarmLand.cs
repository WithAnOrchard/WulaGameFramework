using System;

namespace Farm.Data
{
    [Serializable]
    public class FarmLand
    {
        public int index;

        public Plant plant;

        public int maxWet;

        public int currentWet;

        public FarmLand(int index, Plant plant, int maxWet, int currentWet)
        {
            this.index = index;
            this.plant = plant;
            this.maxWet = maxWet;
            this.currentWet = currentWet;
        }
    }
}