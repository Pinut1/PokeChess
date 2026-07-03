//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;


namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Chess/Cell Occupant Is Not Same Team")]
    public class CellOccupantIsNotSameTeam : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            return PropertyUtil.IsSameTeam(action.Entity.GameGrid.Entities.FirstOrDefault(otherEntity => otherEntity.Cell == cell), action.Entity);
        }
    }
}