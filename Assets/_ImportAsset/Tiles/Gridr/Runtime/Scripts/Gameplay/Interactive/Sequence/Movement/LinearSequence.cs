//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Scripts.Gameplay
{
    
    [CreateAssetMenu(menuName = "Gridr/Sequences/Default/Linear Sequence")]
    public class LinearSequence : MovementSequence
    {

        public float travelTime = .3f;
        
        public override IEnumerator Run(GridEntity entity, Stack<Cell> path, OnSequenceStarted onSequenceStarted, OnSequenceCompleted onSequenceCompleted)
        {
            onSequenceStarted?.Invoke();

            yield return TweenUtil.TweenFullDistance(entity.transform, path.Select(c => c.transform), travelTime, null);
            
            onSequenceCompleted?.Invoke();

        }
    }
}