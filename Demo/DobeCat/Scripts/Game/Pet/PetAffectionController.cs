using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// Tracks pet affection (0–100). Persisted via PlayerPrefs.
    /// DESIGN.md §5.1 — affection stub; extended by petting/feeding interactions.
    /// </summary>
    public class PetAffectionController : MonoBehaviour
    {
        public static PetAffectionController Instance { get; private set; }

        private const string PREFS_KEY = "DobeCat_Affection";

        private float _affection;
        private float _saveTimer;

        public float Affection    => _affection;
        public int   AffectionInt => Mathf.RoundToInt(_affection);

        private void Awake()
        {
            Instance   = this;
            _affection = PlayerPrefs.GetFloat(PREFS_KEY, 0f);
        }

        private void Update()
        {
            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer >= 60f) { _saveTimer = 0f; Save(); }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; Save(); }

        /// <summary>Increase affection by <paramref name="amount"/> (clamped to [0,100]).</summary>
        public void Add(float amount)
        {
            _affection = Mathf.Clamp(_affection + amount, 0f, 100f);
        }

        private void Save() => PlayerPrefs.SetFloat(PREFS_KEY, _affection);
    }
}
