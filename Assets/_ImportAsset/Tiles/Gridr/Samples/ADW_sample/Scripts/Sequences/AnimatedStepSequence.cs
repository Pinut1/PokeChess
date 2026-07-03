//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using System.Collections.Generic;
using Gridr.Extensions;
using Gridr.Gameplay;
using Gridr.Utils;
using Scripts.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Sequences/ADW/Animated Step Sequence")]
    public class AnimatedStepSequence : MovementSequence
    {
        [SerializeField] private AnimationBank animationBank;
        public float stepTime = .2f;
        
        public override IEnumerator Run(GridEntity entity, Stack<Cell> path, OnSequenceStarted onSequenceStarted, OnSequenceCompleted onSequenceCompleted)
        {
            onSequenceStarted?.Invoke();
            var animator = entity.GetComponent<Animator>();
            animator.CrossFade(animationBank.GetMovementClip(PropertyUtil.GetProperty<GridTeamProperty>(entity).team).name, 0, 0);
            
            while (!path.IsEmpty())
            {
                MoveToNextPosition();
                yield return new WaitForSeconds(stepTime);
            }
            
            animator.CrossFade(animationBank.GetIdleClip(PropertyUtil.GetProperty<GridTeamProperty>(entity).team).name, 0, 0);
            onSequenceCompleted?.Invoke();

            void MoveToNextPosition() => entity.transform.position = path.Pop().GroundPoint.position + entity.PositionOffset;
        }
    }
}