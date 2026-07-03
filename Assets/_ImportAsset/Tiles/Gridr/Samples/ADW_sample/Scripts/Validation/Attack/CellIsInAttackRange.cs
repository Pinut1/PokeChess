//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell is in Attack Range")]
    public class CellIsInAttackRange : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is AttackAction attackAction))
                return false;

            var pos = cell.gridPosition - action.Entity.GridPosition;
            var zero = pos.position.x == 0 && pos.position.y == 0;
            var xLess = Mathf.Abs(pos.position.x) <= attackAction.attackData.range;
            var yLess = Mathf.Abs(pos.position.y) <= attackAction.attackData.range;
            var xyLess = Mathf.Abs(pos.position.y) + MathF.Abs(pos.position.x) <= attackAction.attackData.range;

            return !zero && xLess && yLess && xyLess;
        }
    }
}