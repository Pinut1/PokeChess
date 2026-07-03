//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay
{
    public class MobileFriendlyInputSettings : IActionInputSettings
    {
        
        ///<summary>
        /// Moves to next action in queue
        ///</summary>
        public State HandleSelfSelected(GridAction nextAction)
        {
            return nextAction != null ? nextAction.Get() : null;
        }

        ///<summary>
        /// Deselects entity and returns null
        ///</summary>
        public State HandleOtherSelected(GridAction action)
        {
            action.Entity.Deselect();
            return null;
        }

        ///<summary>
        /// Deselects entity and selects the cell
        ///</summary>
        public State HandleCellSelected(GridAction action, Cell cell)
        {
            action.Entity.Deselect();
            return cell.Select();
        }
        
        ///<summary>
        /// Deselects entity and returns null
        ///</summary>
        public State HandleNullSelected(GridAction action)
        {
            if (action == null || action.Entity == null) 
                return null;
           
            action.Close();
            action.Entity.Deselect();
            return null;
        }

        ///<summary>
        /// Returns state of next action
        ///</summary>
        public State HandleNextState(GridAction nextAction)
        {
            return nextAction == null ? null : nextAction.Get();
        }
        
    }
}