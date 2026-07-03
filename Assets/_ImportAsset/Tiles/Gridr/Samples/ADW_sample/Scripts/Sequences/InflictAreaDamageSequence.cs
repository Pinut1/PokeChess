//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections;
using System.Linq;
using Gridr.Extensions;
using Gridr.Gameplay;
using Gridr.Utils;
using Scripts.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Sequences/ADW/Attack Area Sequence")]
    public class InflictAreaDamageSequence : AttackSequence
    {
        [SerializeField] private AnimationBank animationBank;
        [SerializeField] private GameObject visualEffect;
        
        private Animator _animator;

        public override IEnumerator Run(GridEntity attackingEntity, IAttackTarget attackTarget, OnSequenceStarted onSequenceStarted,
            OnSequenceCompleted onSequenceCompleted)
        {
            onSequenceStarted?.Invoke();
            
            SetAttackSpriteDirection();

            GameObject explosion;
            var attackAction = (AttackAction)attackingEntity.FindAction(typeof(AttackAction));
            var inRangeCells = attackAction.Entity.GameGrid.cells.Where(cell => cell.gridPosition.InRange(attackTarget.GetCell().gridPosition, attackAction.attackData.areaOfEffect.area));
            
            if (attackAction)
            {
                _animator = attackingEntity.GetComponent<Animator>();
                _animator.CrossFade(animationBank.GetAttackClip(PropertyUtil.GetProperty<GridTeamProperty>(attackingEntity).team).name, 0, 0);
                if (visualEffect)
                {
                    explosion = Instantiate(visualEffect, attackTarget.GetCell().transform.position, Quaternion.identity);
                    Destroy(explosion, _animator.GetCurrentAnimatorClipInfo(0).Length);
                    foreach (var areaCell in inRangeCells)
                    {
                        var areaExplosion = Instantiate(visualEffect, areaCell.transform.position, Quaternion.identity);
                        Destroy(areaExplosion, _animator.GetCurrentAnimatorClipInfo(0).Length);
                    }
                }
                
                yield return new WaitForSeconds(_animator.GetCurrentAnimatorClipInfo(0).Length);
                attackTarget.Damage(attackAction.attackData.damageAmount);
                foreach (var areaCell in inRangeCells)          
                {
                    foreach (var entity in areaCell.Occupants)
                    {
                        var areaTarget = PropertyUtil.GetProperty<HealthProperty>(entity);
                        if(areaTarget != null)
                            areaTarget.Damage(attackAction.attackData.damageAmount);
                    }
                }
            }
            
            _animator.CrossFade(animationBank.GetIdleClip(PropertyUtil.GetProperty<GridTeamProperty>(attackingEntity).team).name, 0, 0);
            
            SetSpriteDirection();
            onSequenceCompleted?.Invoke();
            
            
            void SetAttackSpriteDirection() =>   attackingEntity.GetComponent<SpriteRenderer>().flipX = (attackingEntity.Cell.gridPosition - attackTarget.GetCell().gridPosition).position.x > 0;
            void SetSpriteDirection() => attackingEntity.GetComponent<SpriteRenderer>().flipX = PropertyUtil.GetProperty<GridTeamProperty>(attackingEntity).team.teamDirection == TeamDirection.Left;
        }
        
    }
}