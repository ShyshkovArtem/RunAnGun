namespace RunGun.Weapons
{
    public interface IWeaponHitReceiver
    {
        void ReceiveWeaponHit(WeaponHitInfo hitInfo);
    }
}
