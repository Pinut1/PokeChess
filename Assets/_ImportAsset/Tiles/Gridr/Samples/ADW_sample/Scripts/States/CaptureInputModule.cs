//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Adw
{
    [CreateAssetMenu(menuName = "Gridr/Input Module/ADW/Confirm Capture")]
    public class CaptureInputModule : InputModule
    {
        public override State Get(GridAction action)
        {
            return Initialized ? new ConfirmCaptureState(action as CaptureAction, actionInputSettings, inputReader, listener, onStartInputState) : null;
        }
    }
}