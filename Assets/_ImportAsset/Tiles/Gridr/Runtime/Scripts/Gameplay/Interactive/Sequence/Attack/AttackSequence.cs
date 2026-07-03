//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using Gridr.Gameplay;
using UnityEngine;

namespace Scripts.Gameplay
{
    public abstract class AttackSequence : ScriptableObject
    {
        public delegate void OnSequenceCompleted();
        public delegate void OnSequenceStarted();
        
        public abstract IEnumerator Run(GridEntity attackingEntity, IAttackTarget attackTarget, OnSequenceStarted onSequenceStarted, OnSequenceCompleted onSequenceCompleted);
    }
}