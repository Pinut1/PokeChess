//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;
using UnityEngine.Events;

namespace Gridr.Gameplay.Events
{
    public abstract class GameEventListener<T> : MonoBehaviour
    {
        public UnityEvent<T> response = new UnityEvent<T>();
        public void Respond(T data) => response?.Invoke(data);
    }
}