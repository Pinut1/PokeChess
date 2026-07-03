//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using System.Collections.Generic;
using Gridr.Extensions;
using Gridr.Gameplay;
using UnityEngine;

namespace Scripts.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Sequences/Default/Step Sequence")]
    public class StepSequence : MovementSequence
    {
        public float stepTime = .2f;
        
        public override IEnumerator Run(GridEntity entity, Stack<Cell> path, OnSequenceStarted onSequenceStarted, OnSequenceCompleted onSequenceCompleted)
        {
            onSequenceStarted?.Invoke();
            
            while (!path.IsEmpty())
            {
                MoveToNextPosition();
                yield return new WaitForSeconds(stepTime);
            }
            
            onSequenceCompleted?.Invoke();

            void MoveToNextPosition() => entity.transform.position = path.Pop().GroundPoint.position + entity.PositionOffset;
        }
    }
}