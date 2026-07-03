//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// A collection of callbacks that can be invoked from an asset file 
    ///</summary>
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/Entity Callbacks")]
    public class EntityCallbacks : ScriptableObject
    {
        public List<EntityCallback> callbacks;
        
        ///<summary>
        /// Invokes all callbacks in the callbacks list. Optionally add onStarted, onCompleted to insert callbacks before or after serialized callbacks
        ///</summary>
        public EntityCallbacks InvokeAll(GridEntity entity, Player player = null, GridAction action = null,
            GridProperty property = null, Cell cell = null, Action onStarted = null, Action onCompleted = null)
        {
            onStarted?.Invoke();
            
            if(callbacks != null && callbacks.Any())
                callbacks?.ForEach(callback => callback.Invoke(entity, player, action, property, cell));
            
            onCompleted?.Invoke();
            
            return this;
        }

        public EntityCallbacks AlsoInvoke(Action callback)
        {
            callback?.Invoke();
            return this;
        }
    }
}