//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay.Events
{
    public class StateEventListener : GameEventListener<State>
    {
        public StateGameEvent gameEvent;
        public void OnEnable() => gameEvent.AddListener(this);
        public void OnDisable() => gameEvent.RemoveListener(this);
    }
}