//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell is Not in Deadzone")]
    public class CellIsOutsideDeadzone : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!(action is AttackAction attackAction))
                return false;
            
            var pos =  cell.gridPosition - action.Entity.GridPosition;
            
            return Mathf.Abs(pos.position.x) + Mathf.Abs(pos.position.y) > attackAction.attackData.deadZone;
        }
    }
}