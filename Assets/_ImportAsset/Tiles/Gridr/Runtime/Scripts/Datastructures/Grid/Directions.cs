using UnityEngine;

//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Datastructures
{
    [CreateAssetMenu(menuName = "Gridr/Cell/Directions")]
    public class Directions : ScriptableObject
    {
        public Direction[] directions;
    }
    
}