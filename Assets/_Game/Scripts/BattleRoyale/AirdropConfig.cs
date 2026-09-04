using UnityEngine;

namespace BattleRoyale
{
    [CreateAssetMenu(menuName = "Battle Royale/Airdrop Config", fileName = "AirdropConfig")]
    public sealed class AirdropConfig : ScriptableObject
    {
        [SerializeField, Min(5f)] private float firstDropDelay = 180f;
        [SerializeField, Min(10f)] private float minInterval = 150f;
        [SerializeField, Min(10f)] private float maxInterval = 240f;
        [SerializeField, Range(1, 4)] private int maxActiveDrops = 2;
        [SerializeField, Min(10f)] private float planeSpeed = 55f;
        [SerializeField, Min(2f)] private float crateFallSpeed = 18f;
        [SerializeField, Min(1f)] private float parachuteFallSpeed = 6f;
        [SerializeField, Min(30f)] private float crateLifetime = 240f;
        [SerializeField] private bool requireSafeZone = true;
        [SerializeField] private bool enableDebug;

        public float FirstDropDelay => Mathf.Max(5f, firstDropDelay);
        public float MinInterval => Mathf.Max(10f, Mathf.Min(minInterval, maxInterval));
        public float MaxInterval => Mathf.Max(MinInterval, maxInterval);
        public int MaxActiveDrops => Mathf.Clamp(maxActiveDrops, 1, 4);
        public float PlaneSpeed => Mathf.Max(10f, planeSpeed);
        public float CrateFallSpeed => Mathf.Max(2f, crateFallSpeed);
        public float ParachuteFallSpeed => Mathf.Max(1f, parachuteFallSpeed);
        public float CrateLifetime => Mathf.Max(30f, crateLifetime);
        public bool RequireSafeZone => requireSafeZone;
        public bool EnableDebug => enableDebug;

        public static AirdropConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<AirdropConfig>();
            config.hideFlags = HideFlags.DontSave;
            return config;
        }
    }
}
