using System.Collections.Generic;
using CharacterManager.Entity;
using UnityEngine;

namespace EssSystem.EssManager.CharacterManager.Entity
{
    public class ModelGameObject : MonoBehaviour
    {
        public List<ModelPartGameObject> modelParts = new();
    }
}