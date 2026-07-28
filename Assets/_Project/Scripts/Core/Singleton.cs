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

    /// <summary>
    /// 로그를 남기지 않고 인스턴스 존재만 확인한다.
    /// Instance 게터는 null일 때 LogError를 찍으므로 "있으면 쓰고 없으면 넘어간다"는
    /// 호출부가 Instance로 널 검사를 하면 검사 자체가 에러 로그가 된다.
    /// 초기화 순서를 보장할 수 없는 곳(UI의 OnEnable 등)에서는 이쪽을 쓸 것.
    /// </summary>
    public static bool HasInstance => _instance != null;

    /// <summary>로그 없이 인스턴스를 가져온다. 없으면 false.</summary>
    public static bool TryGet(out T instance)
    {
        instance = _instance;
        return _instance != null;
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
