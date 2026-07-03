//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using Gridr.Gameplay;
using Gridr.Gameplay.Events;
using UnityEngine;


namespace Gridr.Chess
{
    [CreateAssetMenu(menuName = "Gridr/Entity Callbacks/Chess/Invoke Action Event")]
    public class InvokeActionEvent : EntityCallback
    {
        [SerializeField] private ActionGameEvent actionEvent;
        public override void Invoke(GridEntity entity, Player player = null, GridAction action = null, GridProperty property = null, Cell cell = null)
        {
            actionEvent.Raise(action);
        }
    }
}