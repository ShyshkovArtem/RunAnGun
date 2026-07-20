using UnityEngine;
using UnityEngine.InputSystem;

namespace RunGun
{
    internal static class InputSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;

            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Mouse mouse && !mouse.enabled)
                    InputSystem.EnableDevice(mouse);
            }
        }
    }
}
