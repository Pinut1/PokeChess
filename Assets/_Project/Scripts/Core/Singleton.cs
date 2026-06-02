using UnityEngine;

/// <summary>
/// 싱글턴 베이스 클래스.
/// 씬 전환 시 파괴되지 않음 (DontDestroyOnLoad).
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError($"[Singleton] {typeof(T)} 인스턴스가 없습니다.");
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
}
