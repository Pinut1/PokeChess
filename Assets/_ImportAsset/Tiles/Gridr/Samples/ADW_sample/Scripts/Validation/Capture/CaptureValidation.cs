//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Action Validation/Capture Validation")]
    public class CaptureValidation : ScriptableObject
    {
        public List<ActionCondition<GridAction>> conditions;

        public bool Validate(Cell cell, CaptureAction captureAction)
        {
            return conditions.All(captureCondition => captureCondition.Validate(cell, captureAction));
        }
    }
}