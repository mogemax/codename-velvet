using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// A colored vignette over one camera only (its split-screen part): a canvas in that camera's space with a
    /// radial gradient image. <see cref="Opacity"/> is set by whoever owns it.
    /// </summary>
    public class ScreenVignette : MonoBehaviour
    {
        private const int TextureSize = 128;
        private static Sprite gradient;

        private Image image;

        public static ScreenVignette Create(Camera target, Color color, string label)
        {
            var holder = new GameObject($"{label} Vignette");
            holder.transform.SetParent(target.transform, false);

            Canvas canvas = holder.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = target;
            canvas.planeDistance = target.nearClipPlane + 0.05f;
            canvas.sortingOrder = 500;

            var imageHolder = new GameObject("Image", typeof(RectTransform));
            imageHolder.transform.SetParent(holder.transform, false);
            var rect = (RectTransform)imageHolder.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            ScreenVignette vignette = holder.AddComponent<ScreenVignette>();
            vignette.image = imageHolder.AddComponent<Image>();
            vignette.image.sprite = Gradient();
            vignette.image.raycastTarget = false;
            vignette.image.color = new Color(color.r, color.g, color.b, 0f);
            vignette.image.enabled = false;
            return vignette;
        }

        public float Opacity
        {
            get => image.color.a;
            set
            {
                Color color = image.color;
                color.a = Mathf.Clamp01(value);
                image.color = color;
                image.enabled = color.a > 0.001f;
            }
        }

        /// <summary>Clear in the middle, solid at the edges (shared by every vignette).</summary>
        private static Sprite Gradient()
        {
            if (gradient != null)
                return gradient;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "Vignette Gradient",
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float u = (x + 0.5f) / TextureSize * 2f - 1f;
                float v = (y + 0.5f) / TextureSize * 2f - 1f;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, Mathf.Sqrt(u * u + v * v)));
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            gradient = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            return gradient;
        }
    }
}
