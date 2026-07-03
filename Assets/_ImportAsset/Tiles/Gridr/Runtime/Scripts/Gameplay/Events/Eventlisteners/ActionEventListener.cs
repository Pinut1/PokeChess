//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay.Events
{
    public class ActionEventListener : GameEventListener<GridAction>
    {
        public ActionGameEvent gameEventListener;
        public void OnEnable() => gameEventListener.AddListener(this);
        public void OnDisable() => gameEventListener.RemoveListener(this);
    }
}