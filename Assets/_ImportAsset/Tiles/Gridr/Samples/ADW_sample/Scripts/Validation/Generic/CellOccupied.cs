//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/Shared/Cell is Occupied")]
    public class CellOccupied : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            return cell.Occupied;
        }
    }
}