//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


namespace Gridr.Gameplay
{
    ///<summary>
    /// Handles transitions from actions to states
    ///</summary>
    public interface IActionInputSettings
    {
        public abstract State HandleSelfSelected(GridAction action);
        public abstract State HandleOtherSelected(GridAction action);
        public abstract State HandleCellSelected(GridAction action, Cell cell);
        public abstract State HandleNullSelected(GridAction action);
        public abstract State HandleNextState(GridAction nextAction);
        
    }
}