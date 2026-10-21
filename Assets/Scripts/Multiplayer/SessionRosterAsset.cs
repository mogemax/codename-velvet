using System;
using UnityEngine;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Shares the session roster between the lobby scene and the level scene.
    /// Both reference this asset in the Inspector — no singleton or static field.
    /// The roster itself is runtime-only (not serialized), so nothing leaks into
    /// the asset file; the lobby clears it every time it opens.
    /// </summary>
    [CreateAssetMenu(menuName = "Multiplayer/Session Roster", fileName = "SessionRoster")]
    public class SessionRosterAsset : ScriptableObject
    {
        [NonSerialized] private SessionRoster roster;

        public SessionRoster Roster => roster ??= new SessionRoster();
    }
}
