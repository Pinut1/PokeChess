//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gridr.Gameplay
{
[CreateAssetMenu(menuName = "Gridr/Action Validation/Generic Action Validation")]
    public class ActionValidation : ScriptableObject
    {
        public List<ActionCondition<GridAction>> conditions;
        
        public bool Validate(Cell cell, GridAction action)
        {
            if (cell == null || action == null)
                return false;
            
            return conditions.All(condition => condition.Validate(cell, action));
        }
    }
}