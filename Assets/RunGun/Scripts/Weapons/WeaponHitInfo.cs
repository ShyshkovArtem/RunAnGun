using UnityEngine;

namespace RunGun.Weapons
{
    public readonly struct WeaponHitInfo
    {
        public WeaponHitInfo(WeaponType weaponType, int impactPower, RaycastHit hit, GameObject source)
        {
            WeaponType = weaponType;
            ImpactPower = Mathf.Max(1, impactPower);
            Hit = hit;
            Source = source;
        }

        public WeaponType WeaponType { get; }
        public int ImpactPower { get; }
        public RaycastHit Hit { get; }
        public GameObject Source { get; }
    }
}
