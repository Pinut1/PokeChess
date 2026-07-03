//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using UnityEngine;


namespace Gridr.Gameplay.Events
{
    [Serializable]
    public class GameEvent<T> : ScriptableObject
    {
        private readonly List<GameEventListener<T>> _listeners = new List<GameEventListener<T>>();
        public void AddListener(GameEventListener<T> listener)
        {
            _listeners.Add(listener);
        }
        public void RemoveListener(GameEventListener<T> listener)
        {
            if(!_listeners.Contains(listener))
                return;
            
            _listeners.Remove(listener);
        }
        public void Raise(T data)
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].Respond(data);
            }
        }

    }
}  