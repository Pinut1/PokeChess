using UnityEngine;

/// <summary>
/// 싱글턴 베이스 클래스.
/// 기본은 씬 전환 시 파괴되지 않음(DontDestroyOnLoad). 단, KeepAcrossScenes를
/// false로 오버라이드하면 씬마다 새로 생성되는 "씬 로컬 싱글턴"으로 동작한다.
/// (예: GameManager는 매니저들을 같은 GameObject에 컴포넌트로 들고 있어, 씬을 넘기면
///  중복 인스턴스가 통째로 Destroy되며 게임플레이 매니저까지 사라지므로 씬 로컬로 둔다.)
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

    /// <summary>true면 DontDestroyOnLoad로 씬 전환에도 유지. 씬 로컬이면 false로 오버라이드.</summary>
    protected virtual bool KeepAcrossScenes => true;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
        if (KeepAcrossScenes) DontDestroyOnLoad(gameObject);
    }
}
