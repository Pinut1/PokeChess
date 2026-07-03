//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell Occupant is Not Same Team")]
    public class OccupantIsNotSameTeam : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!cell.Occupied)
                return true;
            
            foreach (var occupyingEntity in cell.Occupants)
            {
                if (PropertyUtil.IsSameTeam(action.Entity, occupyingEntity))
                    return false;
            }

            return true;
        }
    }
}