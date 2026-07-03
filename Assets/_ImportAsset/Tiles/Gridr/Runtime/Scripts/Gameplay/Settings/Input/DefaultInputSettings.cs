
//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


namespace Gridr.Gameplay
{
    public class DefaultInputSettings : IActionInputSettings
    {
        public State HandleSelfSelected(GridAction action)
        {
            return null;
        }

        public State HandleOtherSelected(GridAction action)
        {
            return null;
        }

        public State HandleCellSelected(GridAction action, Cell cell)
        {
            return cell.Select();
        }

        public State HandleNullSelected(GridAction action)
        {
            return null;
        }

        public State HandleNextState(GridAction nextAction)
        {
            return null;
        }
        
    }
}