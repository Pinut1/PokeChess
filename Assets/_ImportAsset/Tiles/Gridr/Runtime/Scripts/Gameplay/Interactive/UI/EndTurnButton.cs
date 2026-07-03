//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;


namespace Gridr.Gameplay
{
    
    public class EndTurnButton : GridButton
    {
        [SerializeField] private TurnManager turnManager;
        
        public override State Select() => new EndingTurnState(0, turnManager);
        public override void Deselect() { }
        public override int GetPriority() => selectionPriority;
    }
}