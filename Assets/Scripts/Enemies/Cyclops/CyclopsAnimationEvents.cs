using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Animation Events only reach components on the Animator's GameObject. The cyclops' Animator lives on its
    /// tilted <c>Visual</c> child, so this forwards the clip events to the <see cref="CyclopsBrain"/> on the root.
    /// </summary>
    public class CyclopsAnimationEvents : MonoBehaviour
    {
        private CyclopsBrain brain;

        private void Awake() => brain = GetComponentInParent<CyclopsBrain>();

        /// <summary>Called by the stomp clip when the foot hits the ground.</summary>
        public void AnimationEvent_Impact()
        {
            if (brain != null) brain.AnimationEvent_Impact();
        }
    }
}
