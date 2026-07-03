//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay.Events
{
    public class EntityEventListener : GameEventListener<GridEntity>
    {
        public EntityGameEvent gameEvent;
        public void Awake() => gameEvent.AddListener(this);
        public void OnDisable() => gameEvent.RemoveListener(this);
    }
}