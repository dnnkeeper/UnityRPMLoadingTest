using System.Collections.Generic;
using ReadyPlayerMe.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace ReadyPlayerMe.QuickStart
{
    public class RPMLoader : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("RPM avatar URL or shortcode to load")]
        private string avatarUrl;
        [SerializeField]
        [Tooltip("If true it will try to load avatar from avatarUrl on start")]
        private bool loadOnStart = true;
        float progress;

        public UnityEvent<float> onLoadingProgress;

        public UnityEvent<string> onLoadingFailed;

        public UnityEvent<GameObject> onAvatarLoaded;

        private GameObject loadedAvatar;

        AvatarObjectLoader avatarObjectLoader;
        private void Start()
        {
            if (loadOnStart)
            {
                LoadAvatar(avatarUrl);
            }
        }

        void Init()
        {
            if (avatarObjectLoader == null)
            {
                Debug.Log($"[RPMLoader] Init {this}", this);
                avatarObjectLoader = new AvatarObjectLoader();
                avatarObjectLoader.OnProgressChanged += OnProgress;
                avatarObjectLoader.OnCompleted += OnLoadCompleted;
                avatarObjectLoader.OnFailed += OnLoadFailed;
            }
            else
            {
                Debug.LogWarning($"[RPMLoader] Init {this} already initialized", this);
            }
        }
        void OnProgress(object sender, ProgressChangeEventArgs args)
        {
            if (Mathf.Abs(progress - args.Progress) < 0.01f)
                return;
            progress = args.Progress;
            if (Application.isEditor)
            {
                Debug.Log("[RPMLoader] OnProgress " + args.Progress.ToString("0.00"), this);
            }
            onLoadingProgress.Invoke(args.Progress);
        }
        [ContextMenu("LoadAvatar")]
        public void LoadAvatar()
        {
            LoadAvatar(avatarUrl);
        }
        public void LoadAvatar(string url)
        {
            avatarObjectLoader = null;

            if (string.IsNullOrEmpty(url))
            {
                if (string.IsNullOrEmpty(avatarUrl))
                {
                    Debug.LogWarning("[RPMLoader] Empty URL", this);
                    onLoadingFailed.Invoke("Empty URL");
                    return;
                }
                url = avatarUrl;
            }

            url = url.Trim(' ');

            if (!url.EndsWith(".glb"))
            {
                Debug.LogError("[RPMLoader] Invalid URL. Must be a .glb file", this);

                return;
            }

            gameObject.SetActive(true);

            if (avatarObjectLoader == null)
            {
                Init();
            }

            avatarUrl = url;

            avatarObjectLoader.LoadAvatar(avatarUrl);
        }

        public GameObject GetLoadedAvatar()
        {
            return loadedAvatar;
        }

        public void RemoveAvatar()
        {
            if (loadedAvatar != null)
            {
                GameObject.Destroy(loadedAvatar);
                loadedAvatar = null;
            }
        }

        int failCount;
        private void OnLoadFailed(object sender, FailureEventArgs args)
        {
            failCount++;
            Debug.LogError($"{args.Message} \n {args.Url}", this);

            if (failCount > 3)
            {
                failCount = 0;
                onLoadingFailed.Invoke(args.Message);
            }
            else
            {
                var fractions = args.Url.Split('/', '.');
                if (fractions.Length > 2)
                {
                    var guid = fractions[fractions.Length - 2];
                    Debug.LogWarning($"[RPMLoader] Avatar DeleteAvatarFolder {guid}", this);
#if UNITY_WEBGL
                    try
                    {
                        WebGLDeleteAvatarFolder(guid);
                    }
                    catch(System.Exception e)
                    {
                        Debug.LogError(e);
                    }
#else
                    AvatarCache.DeleteAvatarFolder(guid);
#endif
                }
                else
                {
                    Debug.LogError($"[RPMLoader] Avatar guid couldn't be found in url: {args.Url} ", this);
                }

                //Retry loading
                LoadAvatar();
            }

            onLoadingFailed.Invoke(args.Message);
        }

        private void OnLoadCompleted(object sender, CompletionEventArgs args)
        {
            Debug.Log("[RPMLoader] OnLoadCompleted", this);

            failCount = 0;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            if (args.Metadata.BodyType != BodyType.FullBody && args.Metadata.BodyType != BodyType.FullBodyXR)
            {
                Debug.LogError("[RPMLoader] Avatar is not FullBody type", this);
                onLoadingFailed.Invoke("Avatar is not FullBody type");
                GameObject.DestroyImmediate(args.Avatar);
                return;
            }
            args.Avatar.transform.SetParent(transform);
            args.Avatar.transform.localPosition = Vector3.zero;
            args.Avatar.transform.localRotation = Quaternion.identity;
            args.Avatar.transform.localScale = Vector3.one;
            loadedAvatar = args.Avatar;
            onAvatarLoaded.Invoke(args.Avatar);
        }

#if UNITY_WEBGL
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebGLDeleteAvatarFolder(string guid);
#endif
    }
}
