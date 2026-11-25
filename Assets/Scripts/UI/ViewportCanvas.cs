using UnityEngine;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// Keeps a screen-overlay canvas on one camera's part of the screen (split screen): its content lives in a
    /// "Viewport" child fitted to the camera's rect, and the canvas scale follows that rect, so a view half as
    /// tall or wide gets UI half the size. Overlay (not camera space) so post-processing never tints the UI.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ViewportCanvas : MonoBehaviour
    {
        private Canvas canvas;
        private Camera view;
        private float referenceHeight;
        private Rect appliedRect;
        private int appliedScreenHeight = -1;

        public RectTransform Viewport { get; private set; }

        /// <summary>
        /// Fits <paramref name="target"/> to <paramref name="viewCamera"/> (null = full screen), moving its current
        /// children into the viewport. Sizes are pixels at <paramref name="referenceScreenHeight"/> on a full screen
        /// (0 = pixels as authored, scale 1 on a full screen). A CanvasScaler on it is turned off: this sets the scale.
        /// </summary>
        public static ViewportCanvas Fit(Canvas target, Camera viewCamera, float referenceScreenHeight)
        {
            if (target.TryGetComponent(out UnityEngine.UI.CanvasScaler scaler))
                scaler.enabled = false;

            var fit = target.gameObject.AddComponent<ViewportCanvas>();
            fit.canvas = target;
            fit.view = viewCamera;
            fit.referenceHeight = referenceScreenHeight;

            var holder = new GameObject("Viewport", typeof(RectTransform));
            fit.Viewport = (RectTransform)holder.transform;
            for (int i = target.transform.childCount - 1; i >= 0; i--)
                target.transform.GetChild(i).SetParent(fit.Viewport, false);
            fit.Viewport.SetParent(target.transform, false);
            fit.Apply();
            return fit;
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            Rect rect = view != null ? view.rect : new Rect(0f, 0f, 1f, 1f);
            if (rect == appliedRect && Screen.height == appliedScreenHeight)
                return;
            appliedRect = rect;
            appliedScreenHeight = Screen.height;

            Viewport.anchorMin = rect.min;
            Viewport.anchorMax = rect.max;
            Viewport.offsetMin = Viewport.offsetMax = Vector2.zero;
            float fullScreenScale = referenceHeight > 0f ? Screen.height / referenceHeight : 1f;
            canvas.scaleFactor = Mathf.Max(0.01f, fullScreenScale * Mathf.Min(rect.width, rect.height));
        }
    }
}
