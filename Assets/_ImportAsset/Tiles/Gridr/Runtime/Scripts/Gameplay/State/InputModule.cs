//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    public abstract class InputModule : ScriptableObject
    {
        public EntityCallbacks onStartInputState;

        protected IInputReader inputReader;
        protected IListener<ISelectable> listener;
        protected IActionInputSettings actionInputSettings;

        public bool Initialized => inputReader != null && listener != null && actionInputSettings != null;
        
        public void Initialize(IInputReader inputReader, IListener<ISelectable> listener, IActionInputSettings actionInputSettings)
        {
            this.inputReader = inputReader;
            this.listener = listener;
            this.actionInputSettings = actionInputSettings;
        }
        
        public abstract State Get(GridAction action);
    }
}