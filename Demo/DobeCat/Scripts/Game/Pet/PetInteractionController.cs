using Demo.DobeCat.Game;
using Demo.DobeCat.Sys;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// Handles left-click and long-press (petting) interactions on the pet.
    /// DESIGN.md §6: click reaction (bubble) + petting (affection).
    /// Uses Win32 GetAsyncKeyState on Windows builds for click-through compatibility.
    /// </summary>
    public class PetInteractionController : MonoBehaviour
    {
        [Tooltip("Hold duration (seconds) before petting starts.")]
        [SerializeField] private float _pettingThreshold = 1f;

        [Tooltip("Affection gained per second while petting.")]
        [SerializeField] private float _affectionPerSecond = 2f;

        [Tooltip("Minimum seconds between click-reaction bubbles.")]
        [SerializeField] private float _reactionCooldown = 2f;

        private PetDragger             _dragger;
        private PetView                _view;
        private PetAffectionController _affection;

        private bool  _wasDown;
        private float _holdTime;
        private bool  _pettingActive;
        private float _cooldownTimer;

        private static readonly string[] ClickPhrases =
        {
            "喵！", "呀！", "嗯？", "别戳我啦～", "你好呀～",
            "*歪头*", "摸什么呢…", "喵喵喵！",
        };

        private static readonly string[] PettingPhrases =
        {
            "呼噜呼噜～", "嗯嗯嗯…好舒服～", "再摸一下嘛～", "呼～",
        };

        private void Awake()
        {
            _dragger   = GetComponent<PetDragger>();
            _view      = GetComponent<PetView>();
            _affection = GetComponent<PetAffectionController>();
        }

        private void Update()
        {
            _cooldownTimer -= Time.unscaledDeltaTime;

            var down    = IsLeftDown();
            var screenP = GetCursorPos();
            var hit     = _view != null && HitTest(screenP);
            var dragging = _dragger != null && _dragger.IsDragging;

            if (!_wasDown && down && hit)
            {
                _holdTime     = 0f;
                _pettingActive = false;
            }

            if (_wasDown && down && !dragging)
            {
                _holdTime += Time.unscaledDeltaTime;
                if (!_pettingActive && _holdTime >= _pettingThreshold)
                {
                    _pettingActive = true;
                    var text = DobeCatDialogueContent.Pick(DobeCatDialogueContent.PET) ?? "呼噜呼噜～";
                    PetSpeechBubble.Instance?.Show(text, 3f);
                    PetSoundController.PlayPurr();
                }
                if (_pettingActive)
                    _affection?.Add(_affectionPerSecond * Time.unscaledDeltaTime);
            }

            if (_wasDown && !down)
            {
                if (!dragging && !_pettingActive && hit && _cooldownTimer <= 0f)
                {
                    _cooldownTimer = _reactionCooldown;
                    var text = DobeCatDialogueContent.Pick(DobeCatDialogueContent.CLICK) ?? "喵！";
                    PetSpeechBubble.Instance?.Show(text, 2f);
                    PetSoundController.PlayMeow();
                }
                _holdTime     = 0f;
                _pettingActive = false;
            }

            _wasDown = down;
        }

        private bool HitTest(Vector2 screenPos)
        {
            if (_view == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            var z = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
            return _view.WorldBounds.Contains(new Vector3(world.x, world.y, _view.WorldBounds.center.z));
        }

        private static Vector2 GetCursorPos()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.GetGlobalCursorScreenPos();
#else
            return Input.mousePosition;
#endif
        }

        private static bool IsLeftDown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
#else
            return Input.GetMouseButton(0);
#endif
        }
    }
}
