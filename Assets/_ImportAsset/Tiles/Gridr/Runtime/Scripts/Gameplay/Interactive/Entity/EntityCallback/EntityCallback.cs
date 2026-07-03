//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// A class that can invoke methods from asset files. Use for easy and quick customization of behaviour and setup
    ///</summary>
    public abstract class EntityCallback : ScriptableObject
    {
        public abstract void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null);
    }
}