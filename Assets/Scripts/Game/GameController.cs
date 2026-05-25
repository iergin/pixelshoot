using UnityEngine;
using PixelShoot.Conveyor;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.Game
{
    public enum GameState { Playing, Won, Failed }

    public class GameController : MonoBehaviour
    {
        [SerializeField] private GridController grid;
        [SerializeField] private ConveyorController conveyor;
        [SerializeField] private ReserveController reserve;

        private GameState state = GameState.Playing;

        public GameState State => state;

        public void Bind(GridController g, ConveyorController c, ReserveController r)
        {
            grid = g;
            conveyor = c;
            reserve = r;
            if (grid != null) grid.OnGridCleared += HandleGridCleared;
        }

        /// <summary>
        /// Called by ShooterColumn when the top shooter is clicked.
        /// Returns true if the shooter was accepted (column should remove it). If
        /// the conveyor is full, this is a silent no-op — the shooter stays in the
        /// column. Reserve slots are NOT used for column overflow; they are only
        /// filled by shooters that finish a conveyor lap with shots remaining.
        /// </summary>
        public bool RequestLaunch(Shooter shooter)
        {
            if (state != GameState.Playing) return false;

            if (conveyor.TryReserveSlot(out float boardingDuration, out float landingProgress))
            {
                BoardConveyor(shooter, boardingDuration, landingProgress);
                return true;
            }

            // Conveyor full → ignore the click. User must wait for a slot to free up.
            return false;
        }

        /// <summary>
        /// Called by ShooterClickHandler when a shooter currently sitting in a reserve slot is clicked.
        /// Boards the conveyor if there is space; otherwise no-op.
        /// </summary>
        public void RequestBoardFromReserve(Shooter shooter)
        {
            if (state != GameState.Playing) return;
            if (shooter == null || shooter.State != ShooterState.InReserve) return;

            if (!conveyor.TryReserveSlot(out float boardingDuration, out float landingProgress)) return;

            reserve.FreeSlot(shooter);
            BoardConveyor(shooter, boardingDuration, landingProgress);
        }

        private void BoardConveyor(Shooter shooter, float boardingDuration, float landingProgress)
        {
            conveyor.EvaluatePath(landingProgress, out Vector3 worldPos, out _, out _);
            shooter.OnPathEnded -= HandleRiderPathEnded;
            shooter.OnPathEnded += HandleRiderPathEnded;
            shooter.JumpTo(worldPos, boardingDuration, () =>
            {
                conveyor.RegisterRider(shooter, landingProgress);
            }, ShooterState.OnConveyor);
        }

        /// <summary>Sends a shooter into a free reserve slot. Returns false if reserve is full.</summary>
        private bool SendToReserve(Shooter shooter)
        {
            int idx = reserve.FindFreeSlot();
            if (idx < 0) return false;
            reserve.Occupy(idx, shooter);
            var slotWorld = reserve.GetSlotPosition(idx);
            // The callback lets ReserveController retry any deferred compact once this jump lands.
            shooter.JumpTo(slotWorld, reserve.JumpDuration, reserve.NotifyIncomingLanded, ShooterState.InReserve);
            return true;
        }

        /// <summary>
        /// Shooter completed a full conveyor lap. If it has no shots left, expire.
        /// Otherwise try to park in reserve — if reserve is full while shots remain,
        /// the level fails because those bullets are needed to clear the grid.
        /// </summary>
        private void HandleRiderPathEnded(Shooter shooter)
        {
            shooter.OnPathEnded -= HandleRiderPathEnded;
            conveyor.RemoveRider(shooter);

            if (shooter.ShotsRemaining <= 0)
            {
                shooter.Expire();
                return;
            }

            if (SendToReserve(shooter)) return;

            // Has shots but nowhere to wait → unwinnable.
            shooter.Expire();
            Fail();
        }

        private void HandleGridCleared()
        {
            if (state != GameState.Playing) return;
            state = GameState.Won;
            Debug.Log("LEVEL WON: grid cleared.");
        }

        private void Fail()
        {
            if (state != GameState.Playing) return;
            state = GameState.Failed;
            Debug.Log("LEVEL FAILED: a returning shooter still had shots but reserve was full.");
        }

        private void OnDestroy()
        {
            if (grid != null) grid.OnGridCleared -= HandleGridCleared;
        }
    }
}
