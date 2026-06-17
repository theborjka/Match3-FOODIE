using UnityEngine;

namespace Match3Foodie
{
    [DefaultExecutionOrder(-10000)]
    public sealed class Match3WebGLPerformanceSettings : MonoBehaviour
    {
        [Header("Frame Rate")]
        [SerializeField, Min(30)] private int targetFrameRate = 60;
        [SerializeField, Min(15)] private int editorTargetFrameRate = 60;
        [SerializeField, Min(0)] private int vSyncCount;

        [Header("Time")]
        [SerializeField, Min(0.001f)] private float fixedDeltaTime = 1f / 60f;
        [SerializeField, Min(0.01f)] private float maximumDeltaTime = 1f / 20f;

        [Header("Render Cost")]
        [SerializeField, Range(0.5f, 1f)] private float webGLRenderBufferScale = 1f;

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            targetFrameRate = Mathf.Max(30, targetFrameRate);
            editorTargetFrameRate = Mathf.Max(15, editorTargetFrameRate);
            vSyncCount = Mathf.Max(0, vSyncCount);
            fixedDeltaTime = Mathf.Max(0.001f, fixedDeltaTime);
            maximumDeltaTime = Mathf.Max(0.01f, maximumDeltaTime);
            webGLRenderBufferScale = Mathf.Clamp(webGLRenderBufferScale, 0.5f, 1f);
        }

        [ContextMenu("Apply Performance Settings")]
        public void Apply()
        {
            QualitySettings.vSyncCount = vSyncCount;
            QualitySettings.antiAliasing = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            Application.targetFrameRate = targetFrameRate;
            ScalableBufferManager.ResizeBuffers(webGLRenderBufferScale, webGLRenderBufferScale);
#else
            Application.targetFrameRate = editorTargetFrameRate;
#endif
            Time.fixedDeltaTime = fixedDeltaTime;
            Time.maximumDeltaTime = maximumDeltaTime;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyDefaultWebGLFrameRate()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            Application.targetFrameRate = 60;
            ScalableBufferManager.ResizeBuffers(1f, 1f);
            Time.fixedDeltaTime = 1f / 60f;
            Time.maximumDeltaTime = 1f / 20f;
#endif
        }
    }
}
