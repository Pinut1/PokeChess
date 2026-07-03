//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    
    [CreateAssetMenu(menuName = "Gridr/Action Condition/ADW/Cell is Not Occupied or Type")]
    public class CellIsNotOccupiedOrType : ActionCondition<GridAction>
    {
        [SerializeField] private List<TypeID> validTypes;
        public override bool Validate(Cell cell, GridAction action)
        {
            if (!cell.Occupied)
                return true;

            return cell.Occupants.All(e => validTypes.Contains(e.ID));
        }
    }
}