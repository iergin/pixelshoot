using System;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Global flag for "an interactive booster (Claw / FillColor) is running". While active,
    /// booster buttons ignore taps and the BoosterBar slides away. Controllers set it on
    /// begin and clear it on end/cancel.
    /// </summary>
    public static class BoosterProcess
    {
        public static bool Active { get; private set; }
        public static event Action<bool> Changed;

        public static void Set(bool on)
        {
            if (Active == on) return;
            Active = on;
            Changed?.Invoke(on);
        }
    }
}
