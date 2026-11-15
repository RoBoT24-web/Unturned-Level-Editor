using System;
using SDG.Unturned;
using UnityEngine;

namespace LevelEditorPlus.UI
{
    internal sealed class ObjectAssetPreview : SleekWrapper
    {
        private readonly Texture2D texture;

        private ObjectAssetPreview(Texture2D texture, System.Action onClicked)
        {
            this.texture = texture;
            ISleekImage image = Glazier.Get().CreateImage(texture);
            image.SizeScale_X = 1f;
            image.SizeScale_Y = 1f;
            image.OnClicked += onClicked;
            AddChild(image);
        }

        public static ObjectAssetPreview Create(ObjectAsset asset, int previewSize, System.Action onClicked)
        {
            Texture2D texture = null;
            try
            {
                texture = Render(asset, previewSize);
                return texture == null ? null : new ObjectAssetPreview(texture, onClicked);
            }
            catch (Exception e)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                UnturnedLog.exception(e, $"[LevelEditorPlus] Failed to render preview for {asset.FriendlyName} ({asset.GUID}):");
                return null;
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static Texture2D Render(ObjectAsset asset, int previewSize)
        {
            const int previewLayer = 31;
            int renderSize = Mathf.Max(previewSize, 64);
            int previewLayerMask = 1 << previewLayer;
            GameObject prefab = asset.GetOrLoadModel(isEditor: true);
            if (prefab == null)
            {
                return null;
            }

            GameObject previewRoot = null;
            GameObject previewRig = null;
            Camera camera = null;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            bool keepTexture = false;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                previewRoot = new GameObject("LevelEditorPlus Object Preview");
                previewRoot.hideFlags = HideFlags.HideAndDontSave;
                previewRoot.layer = previewLayer;
                previewRoot.transform.position = new Vector3(0f, 10000f, 0f);
                previewRoot.SetActive(false);

                GameObject instance = UnityEngine.Object.Instantiate(prefab, previewRoot.transform, false);
                instance.name = "Preview Model";
                SetLayerRecursively(instance.transform, previewLayer);
                foreach (Behaviour behaviour in instance.GetComponentsInChildren<Behaviour>(true))
                {
                    behaviour.enabled = false;
                }
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                previewRoot.SetActive(true);

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                bool hasBounds = false;
                Bounds bounds = default(Bounds);
                foreach (Renderer renderer in renderers)
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (hasBounds)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    else
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                }
                if (!hasBounds)
                {
                    return null;
                }

                previewRig = new GameObject("LevelEditorPlus Preview Camera");
                previewRig.hideFlags = HideFlags.HideAndDontSave;
                previewRig.layer = previewLayer;
                camera = previewRig.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = previewLayerMask;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.fieldOfView = 35f;
                camera.aspect = 1f;
                camera.useOcclusionCulling = false;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                Light light = previewRig.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.color = Color.white;
                light.shadows = LightShadows.None;
                light.cullingMask = previewLayerMask;

                Vector3 center = bounds.center;
                float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
                float halfFieldOfView = camera.fieldOfView * Mathf.Deg2Rad * 0.5f;
                float distance = radius / Mathf.Sin(halfFieldOfView) * 1.1f;
                Vector3 direction = new Vector3(1f, 0.7f, -1f).normalized;
                camera.transform.position = center + direction * distance;
                camera.transform.LookAt(center);
                camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.1f);
                camera.farClipPlane = distance + radius * 1.1f;

                renderTexture = RenderTexture.GetTemporary(renderSize, renderSize, 16,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(renderSize, renderSize, TextureFormat.RGBA32, false, false);
                texture.name = $"{asset.GUID:N} Preview";
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.ReadPixels(new Rect(0f, 0f, renderSize, renderSize), 0, 0);
                texture.Apply(false, true);
                keepTexture = true;
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (camera != null)
                {
                    camera.targetTexture = null;
                }
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
                if (texture != null && !keepTexture)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                if (previewRoot != null)
                {
                    previewRoot.SetActive(false);
                    UnityEngine.Object.Destroy(previewRoot);
                }
                if (previewRig != null)
                {
                    previewRig.SetActive(false);
                    UnityEngine.Object.Destroy(previewRig);
                }
            }
        }

        private static void SetLayerRecursively(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            for (int childIndex = 0; childIndex < transform.childCount; ++childIndex)
            {
                SetLayerRecursively(transform.GetChild(childIndex), layer);
            }
        }
    }
}
