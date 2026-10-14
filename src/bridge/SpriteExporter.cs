using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace BongoChatBridge
{
    internal static class SpriteExporter
    {
        // A private offscreen UI canvas can render packed/non-readable sprites
        // using the game's existing UI shader, without changing the source
        // texture, game cameras, or any original asset files.
        public static byte[] Encode(Sprite sprite)
        {
            Rect rect = sprite.rect;
            float scale = Math.Min(1f, 256f / Math.Max(rect.width, rect.height));
            int width = Math.Max(1, (int)(rect.width * scale));
            int height = Math.Max(1, (int)(rect.height * scale));
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.D24_UNorm_S8_UInt);
            RenderTexture target = RenderTexture.GetTemporary(descriptor);
            RenderTexture previous = RenderTexture.active;
            GameObject cameraObject = null, canvasObject = null;
            Texture2D readable = null;
            try
            {
                cameraObject = new GameObject("BongoChat Icon Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(100000, 100000, -10);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0, 0, 0, 0);
                camera.targetTexture = target;
                canvasObject = new GameObject("BongoChat Icon Canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = 31;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.layer = 31;
                iconObject.transform.SetParent(canvasObject.transform, false);
                var image = iconObject.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var transform = (RectTransform)iconObject.transform;
                transform.anchorMin = new Vector2(0.5f, 0.5f);
                transform.anchorMax = new Vector2(0.5f, 0.5f);
                transform.anchoredPosition = Vector2.zero;
                transform.sizeDelta = new Vector2(width, height);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply();
                return ImageConversion.EncodeToPNG(readable);
            }
            finally
            {
                RenderTexture.active = previous;
                if (cameraObject != null)
                {
                    cameraObject.GetComponent<Camera>().targetTexture = null;
                    cameraObject.SetActive(false); UnityEngine.Object.Destroy(cameraObject);
                }
                if (canvasObject != null) { canvasObject.SetActive(false); UnityEngine.Object.Destroy(canvasObject); }
                if (readable != null) UnityEngine.Object.Destroy(readable);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
