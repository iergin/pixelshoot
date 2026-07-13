namespace PixelShoot.Data
{
    /// <summary>The special level mechanics a one-time tutorial can explain.</summary>
    public enum SpecialItem
    {
        Surprise, // "?" bus that reveals when it surfaces
        Link,     // buses chained into a group that board/dissolve together
        LockKey,  // locked buses + the key cells that unlock them
        Bomb,     // a box that clears a 5×5 area when destroyed
    }
}
