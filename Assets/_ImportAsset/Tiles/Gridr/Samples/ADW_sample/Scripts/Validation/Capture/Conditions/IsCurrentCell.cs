//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell is Current Cell")]
    public class IsCurrentCell : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            return cell == action.Entity.Cell;
        }
    }
}