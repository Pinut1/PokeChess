//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    [CreateAssetMenu(menuName = "Gridr/Input Module/Confirm Movement")]
    public class MovementInputModule : InputModule
    {
        public override State Get(GridAction action)
        {
            return !Initialized ? null : new ConfirmMovementState(action as MovementAction, inputReader, listener, actionInputSettings, onStartInputState);
        }
        
    }
}