using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Weapons
{
    public static class WeaponSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnPlayer()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                return;
            }

            var weaponController = player.GetComponent<PlayerWeaponController>();
            if (weaponController == null)
            {
                weaponController = player.gameObject.AddComponent<PlayerWeaponController>();
            }

            var hudController = player.GetComponent<WeaponHudController>();
            if (hudController == null)
            {
                hudController = player.gameObject.AddComponent<WeaponHudController>();
            }

            hudController.Bind(weaponController);
        }
    }
}
