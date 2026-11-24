namespace CodenameVelvet.Cameras
{
    /// <summary>
    /// Pitch the player cameras look down at, written by <see cref="CameraFollow"/>. Sprites lean back by the
    /// same angle (<see cref="ViewTilt"/>) so they face the camera instead of looking squashed.
    /// </summary>
    public static class ViewAngle
    {
        public const float DefaultPitch = 18f;

        public static float Pitch { get; set; } = DefaultPitch;
    }
}
