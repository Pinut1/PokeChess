//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Capture Target Is Not Same Team")]
    public class CaptureableIsNotSameTeam : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            var occupantsOfDifferentTeam = cell.Occupants.Where(o => !PropertyUtil.IsSameTeam(o, action.Entity));
            foreach (var entity in occupantsOfDifferentTeam)
            {
                if (PropertyUtil.GetProperty<CaptureTargetProperty>(entity) != null)
                    return true;
            }

            return false;
        }
    }
}