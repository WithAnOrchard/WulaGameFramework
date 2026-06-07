using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Presentation.InputManager
{
    [Serializable]
    public class InputDefaultConfigFile
    {
        public List<InputActionBindingDefinition> Bindings = new();
    }

    [Serializable]
    public class InputActionBindingDefinition
    {
        public string Action;
        public List<string> Keys = new();

        public KeyCode[] ToKeyCodes(Action<string> warn = null)
        {
            if (Keys == null || Keys.Count == 0) return null;

            var result = new List<KeyCode>(Keys.Count);
            foreach (var keyName in Keys)
            {
                if (string.IsNullOrWhiteSpace(keyName)) continue;
                if (Enum.TryParse<KeyCode>(keyName, out var key))
                {
                    result.Add(key);
                }
                else
                {
                    warn?.Invoke($"Invalid KeyCode '{keyName}' for input action '{Action}'");
                }
            }

            return result.Count == 0 ? null : result.ToArray();
        }
    }
}
