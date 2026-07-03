//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using System.Collections.Generic;
using Gridr.Gameplay;
using UnityEngine;

namespace Scripts.Gameplay
{
    public abstract class MovementSequence : ScriptableObject
    {
        public delegate void OnSequenceCompleted();
        public delegate void OnSequenceStarted();
        
        public abstract IEnumerator Run(GridEntity entity, Stack<Cell> path, OnSequenceStarted onSequenceStarted, OnSequenceCompleted onSequenceCompleted);
    }
}