using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.ResourceProviders;

public class AddressableLoader : MonoBehaviour
{
    public bool dontDestroyOnLoad;

    private void Awake()
    {
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    public async Task<GameObject> Instantiate(AssetReference assetReference)
    {
        var op = await AddressableLoader.InstantiateAssetAsync(assetReference.RuntimeKey.ToString());

        return op;
    }
    public async Task<GameObject> Instantiate(string assetLabel)
    {
        var op = await AddressableLoader.InstantiateAssetAsync(assetLabel);

        return op;
    }
    public static async Task<GameObject> InstantiateAssetAsync(string assetLabel)
    {
        // Load the Addressable asset asynchronously.
        AsyncOperationHandle<GameObject> instantiateAsyncHandle = Addressables.InstantiateAsync(assetLabel);

        await instantiateAsyncHandle.Task;

        return instantiateAsyncHandle.Task.Result;
    }

    public void Load(AssetReference sceneAssetReference)
    {
        _ = AddressableLoader.LoadSceneAsync(sceneAssetReference.RuntimeKey.ToString());
    }
    public void Load(string sceneLabel)
    {
        _ = AddressableLoader.LoadSceneAsync(sceneLabel);
    }
    public static async Task<SceneInstance> LoadSceneAsync(string sceneLabel)
    {
        var loadResourceLocationHandle = Addressables.LoadResourceLocationsAsync(sceneLabel);

        await loadResourceLocationHandle.Task;

        var loadSceneAsyncHandle = Addressables.LoadSceneAsync(sceneLabel);

        await loadSceneAsyncHandle.Task;

        return loadSceneAsyncHandle.Task.Result;
    }

}
