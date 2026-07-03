//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Samples.CHEX_sample
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/CHEX/Cell is in Attack Range Hex")]
    
    public class CellIsInAttackRangeHex : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is AttackAction attackAction))
                return false;

            var pos = cell.gridPosition - action.Entity.GridPosition;
            var zero = pos.position.x == 0 && pos.position.y == 0 && pos.position.z == 0;
            var xLess = Mathf.Abs(pos.position.x) <= attackAction.attackData.range;
            var yLess = Mathf.Abs(pos.position.y) <= attackAction.attackData.range;
            var zLess = Mathf.Abs(pos.position.z) <= attackAction.attackData.range;

            return !zero && xLess && yLess && zLess;        
        }
    }
}