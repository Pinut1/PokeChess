//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    public abstract class PlayerInputModule : ScriptableObject
    {
        public abstract State Get(Player pLayer);
    }
}