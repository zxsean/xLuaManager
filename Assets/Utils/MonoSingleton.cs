using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    protected static T _instance = null;

    public static T GetInstance()
    {
        if (_instance == null)
        {
            _instance = FindObjectOfType<T>();

            if (FindObjectsOfType<T>().Length > 1)
            {
                Debug.LogError("More than 1!");

                return _instance;
            }

            if (_instance == null)
            {
                string instanceName = typeof(T).Name;

                Debug.LogWarning("Instance Name: " + instanceName);

                GameObject _instanceGo = GameObject.Find(instanceName);

                if (_instanceGo == null)
                {
                    _instanceGo = new GameObject(instanceName);
                }

                _instance = _instanceGo.AddComponent<T>();

                if (Application.isPlaying)
                    DontDestroyOnLoad(_instanceGo);

                // 保证实例不会被释放
                Debug.LogWarning("Add New Singleton " + _instance.name + " in Game!");
            }
            else
            {
                Debug.Log("Already exist: " + _instance.name);
            }
        }

        return _instance;
    }

    protected virtual void OnDestroy()
    {
        _instance = null;
    }
}