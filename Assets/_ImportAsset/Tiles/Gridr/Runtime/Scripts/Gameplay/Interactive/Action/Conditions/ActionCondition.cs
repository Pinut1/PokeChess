//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    public abstract class ActionCondition<T> : ScriptableObject
    {
        
        [TextArea(15,20)]
        public string description = "";
        
        
        public abstract bool Validate(Cell cell, T action);
    }
}