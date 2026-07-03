//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell Is Capture Target")]
    public class CellIsCaptureable : ActionCondition<GridAction>
    {
        public override bool Validate(Cell cell, GridAction action)
        {
            return cell.Occupants.Any(e => PropertyUtil.GetProperty<CaptureTargetProperty>(e) != null);
        }
    }
}