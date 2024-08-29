using System.Collections.Generic;
using ReadyPlayerMe.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace ReadyPlayerMe.QuickStart
{
    public class RPMAvatarObjectLoader : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("RPM avatar URL or shortcode to load")]
        private string avatarUrl;

        public string LoadedAvatarUrl => loadedAvatarUrl;
        //To keep track of the loaded avatar we serialize references but hide them in the inspector
        [SerializeField, HideInInspector] string loadedAvatarUrl;
        [SerializeField, HideInInspector] List<Transform> avatarParts = new List<Transform>();

        bool isLoading;
        public bool IsLoading => isLoading;

        private AvatarObjectLoader avatarObjectLoader;

        public Animator animatorParent;

        [SerializeField]
        [Tooltip("If true it will try to load avatar from avatarUrl on start")]
        private bool loadOnStart = true;

        public UnityEvent<float> onLoadingProgress;

        public UnityEvent<string> onLoadingFailed;

        public UnityEvent<GameObject> onAvatarLoaded, onAvatarLoadedPostprocess;

        private void Start()
        {
            if (loadOnStart)
            {
                LoadAvatar(avatarUrl);
            }
        }

        public void SetAvatarURL(string url)
        {
            Debug.Log($"[RPMAvatarLoader] {transform.root?.name} SetAvatarURL " + url, this);
            avatarUrl = url;
        }

        void Init()
        {
            if (avatarObjectLoader == null)
            {
                Debug.Log($"[RPMAvatarLoader] Init {this}", this);
                avatarObjectLoader = new AvatarObjectLoader();
                avatarObjectLoader.OnProgressChanged += OnProgress;
                //avatarObjectLoader.OperationCompleted += OnOperationCompleted;
                avatarObjectLoader.OnCompleted += OnLoadCompleted;
                avatarObjectLoader.OnFailed += OnLoadFailed;
            }
            else
            {
                Debug.LogWarning($"[RPMAvatarLoader] Init {this} already initialized", this);
            }
        }

        //void OnOperationCompleted(object sender, ReadyPlayerMe.Core.IOperation<AvatarContext> args)
        //{
        //    Debug.Log($"[RPMAvatarLoader] OnOperationCompleted {args}", this);
        //    isLoading = false;
        //}
        float progress;
        void OnProgress(object sender, ProgressChangeEventArgs args)
        {
            if (Mathf.Abs(progress - args.Progress) < 0.01f)
                return;
            progress = args.Progress;
            if (Application.isEditor)
            {
                Debug.Log("[RPMAvatarLoader] OnProgress " + args.Progress.ToString("0.00"), this);
            }
            onLoadingProgress.Invoke(args.Progress);
        }
        public void UpdateIfAvatarLinkUpdated()
        {
            if (loadedAvatarUrl != avatarUrl)
            {
                Debug.Log("[RPMAvatarLoader] Avatar link updated. Initiate a new loading");
                LoadAvatar();
            }
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
                    Debug.LogWarning("[RPMAvatarLoader] Empty URL", this);
                    onLoadingFailed.Invoke("Empty URL");
                    return;
                }
                url = avatarUrl;
            }
            if (isLoading)
            {
                Debug.LogWarning("[RPMAvatarLoader] avatar loading already in progress");
                return;
            }
            url = url.Trim(' ');
            if (!isActiveAndEnabled)
            {
                SetAvatarURL(url);
                Debug.LogWarning("[RPMAvatarLoader] avatar loading can't be started because component is not active or enabled! Plan loading on start with loadOnStart = true", this);
                loadOnStart = true;
                return;
            }

            if (loadedAvatarUrl != url)
            {
                if (!url.EndsWith(".glb"))
                {
                    Debug.LogError("[RPMAvatarLoader] Invalid URL. Must be a .glb file", this);

                    return;
                }

                gameObject.SetActive(true);

                if (avatarObjectLoader == null)
                {
                    Init();
                }

                //remove any leading or trailing spaces
                SetAvatarURL(url);
                if (string.IsNullOrEmpty(loadedAvatarUrl))
                    Debug.Log("[RPMAvatarLoader] load avatar " + avatarUrl);
                else
                    Debug.Log($"[RPMAvatarLoader] load avatar {avatarUrl} instead of {loadedAvatarUrl}");

                isLoading = true;
                loadedAvatarUrl = avatarUrl;
                avatarObjectLoader.LoadAvatar(avatarUrl);
            }
            else
            {
                Debug.LogWarning($"[RPMAvatarLoader] This avatar {loadedAvatarUrl.Substring(loadedAvatarUrl.LastIndexOf('/') + 1)} already loaded. Call 'Remove Avatar' method");
                onAvatarLoaded.Invoke(animatorParent.gameObject);
                Invoke(nameof(OnAvatarLoadedPostprocess), 0.2f);
            }
        }
        int failCount;
        private void OnLoadFailed(object sender, FailureEventArgs args)
        {
            isLoading = false;
            failCount++;
            Debug.LogError($"{args.Message} \n {args.Url}", this);

            if (failCount > 3)
            {
                //Debug.LogError("[RPMAvatarLoader] Avatar loading failed 3 times. CLEAR CACHE!", this);
                failCount = 0;
                onLoadingFailed.Invoke(args.Message);
                //AvatarCache.Clear();
            }
            else
            {
                var fractions = args.Url.Split('/', '.');
                if (fractions.Length > 2)
                {
                    var guid = fractions[fractions.Length - 2];
                    Debug.LogWarning($"[RPMAvatarLoader] Avatar DeleteAvatarFolder {guid}", this);
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
                    Debug.LogError($"[RPMAvatarLoader] Avatar guid couldn't be found in url: {args.Url} ", this);
                }
                //Retry loading
                loadedAvatarUrl = "";
                LoadAvatar();
            }
            //Debug.LogError("[RPMAvatarLoader] AvatarCache.Clear after error", this);

            //AvatarCache.DeleteAvatarFolder(guid);

            onLoadingFailed.Invoke(args.Message);
        }

        private void OnLoadCompleted(object sender, CompletionEventArgs args)
        {
            Debug.Log("[RPMAvatarLoader] OnLoadCompleted", this);

            failCount = 0;

            isLoading = false;

            loadedAvatarUrl = args.Url;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            if (args.Metadata.BodyType != BodyType.FullBody && args.Metadata.BodyType != BodyType.FullBodyXR)
            {
                Debug.LogError("[RPMAvatarLoader] Avatar is not FullBody type", this);
                onLoadingFailed.Invoke("Avatar is not FullBody type");
                GameObject.DestroyImmediate(args.Avatar);
                return;
            }

            // if (args.Metadata.OutfitGender == OutfitGender.Feminine){
            //     animatorParent.avatar = femaleAvatar;
            // }
            // else{
            //     animatorParent.avatar = maleAvatar;
            // }

            SetupAvatar(args.Avatar);
        }

        [ContextMenu("Remove Avatar")]
        public void RemoveAvatar()
        {

            DestroyAvatarParts();
            loadedAvatarUrl = "";
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            if (animatorParent != null)
                animatorParent.gameObject.SetActive(false);
        }

        void DestroyAvatarParts()
        {
            if (avatarParts.Count > 0)
            {
                foreach (var t in avatarParts)
                {
                    if (t != null)
                        DestroyImmediate(t.gameObject);
                }
                avatarParts.Clear();
            }
        }

        private void SetupAvatar(GameObject targetAvatar)
        {

            DestroyAvatarParts();

            var newAvatarAnimator = targetAvatar.GetComponent<Animator>();
            if (animatorParent == null)
            {
                animatorParent = newAvatarAnimator;

                onAvatarLoaded.Invoke(newAvatarAnimator.gameObject);

                return;
            }

            animatorParent.avatar = newAvatarAnimator.avatar;

            targetAvatar.transform.parent = animatorParent.transform;
            targetAvatar.transform.localScale = Vector3.one;
            for (int i = targetAvatar.transform.childCount - 1; i >= 0; --i)
            {
                Transform child = targetAvatar.transform.GetChild(i);
                child.SetParent(animatorParent.transform, false);
                avatarParts.Add(child);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

            DestroyImmediate(targetAvatar);

            //animatorParent.gameObject.SetActive(true);

            //Debug.Log(animatorParent+" REBIND", animatorParent);

            animatorParent.Rebind();

            //animatorParent.enabled = false;

            //animatorParent.gameObject.SetActive(false);

            //Invoke("EnableAnimator", 0.01f);
            EnableAnimator();

            onAvatarLoaded.Invoke(animatorParent.gameObject);

            Invoke(nameof(OnAvatarLoadedPostprocess), 0.2f);

        }

        void OnAvatarLoadedPostprocess()
        {
            onAvatarLoadedPostprocess.Invoke(animatorParent.gameObject);
        }
        void EnableAnimator()
        {
            //Debug.Log(animatorParent + " EnableAnimator", animatorParent);

            animatorParent.gameObject.SetActive(true);

            animatorParent.enabled = true;
        }

#if UNITY_WEBGL
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebGLDeleteAvatarFolder(string guid);
#endif
    }
}
