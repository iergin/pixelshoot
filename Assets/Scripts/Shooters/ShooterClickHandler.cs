using System;
using UnityEngine;

namespace PixelShoot.Shooters
{
    /// <summary>
    /// Attached to a shooter's collider. Routes the click to the right handler
    /// depending on the shooter's current state.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ShooterClickHandler : MonoBehaviour
    {
        public Shooter Shooter { get; private set; }
        private ShooterColumn column;
        private Action<Shooter> reserveAction;

        public void Configure(Shooter shooter, ShooterColumn column, Action<Shooter> reserveAction)
        {
            this.Shooter = shooter;
            this.column = column;
            this.reserveAction = reserveAction;
        }

        public void NotifyClicked()
        {
            if (Shooter == null) return;
            switch (Shooter.State)
            {
                case ShooterState.InColumn:
                    column?.TryLaunchShooter(Shooter);
                    break;
                case ShooterState.InReserve:
                    reserveAction?.Invoke(Shooter);
                    break;
                // Boarding / OnConveyor / Expired: ignore
            }
        }
    }
}
