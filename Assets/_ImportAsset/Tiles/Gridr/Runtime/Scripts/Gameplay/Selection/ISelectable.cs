//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


namespace Gridr.Gameplay
{
    ///<summary>
    /// Object able to return a State
    ///</summary>
    public interface ISelectable : IPriority
    {
        State Select();
        void Deselect();
    }
}