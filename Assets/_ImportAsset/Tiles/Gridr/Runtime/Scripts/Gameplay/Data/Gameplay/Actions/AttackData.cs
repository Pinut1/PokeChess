//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Action Data/Attack Data")]
    public class AttackData : ScriptableObject
    {
        public int range;
        public int deadZone;
        public float damageAmount;
        public AreaOfEffect areaOfEffect;

    }
    
    [Serializable]
    public class AreaOfEffect
    {
        public int area;
    }
}
