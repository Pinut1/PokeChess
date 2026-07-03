//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


namespace Gridr.Gameplay
{
    public class State
    {
        protected enum Stage
        {
            Enter,
            Update,
            Exit
        }

        protected Stage stage;
        protected State nextState;
        
        ///<summary>
        /// First method that is called and will be called only once
        ///</summary>
        protected void EnterState()
        {
            stage = Stage.Update;
            Enter();
        }

        ///<summary>
        /// Should be the first method that runs when a State is loaded. Prepare your state here
        ///</summary>
        protected virtual void Enter() { }

        ///<summary>
        /// Should implement logic required to proceed to the next state. Runs in the update loop of the game.
        ///</summary>
        protected virtual void Update()
        {
            stage = Stage.Update;
        }

        ///<summary>
        /// Should be last method that runs when a state has come to a conclusion. Make sure to run the base method to return the next state.
        ///</summary>
        protected virtual void Exit()
        {
            stage = Stage.Exit;
        }

        ///<summary>
        /// Handles running state and transition to next state
        ///</summary>
        public virtual State Run()
        {
            switch (stage)
            {
                case Stage.Enter : EnterState();
                    break;
                case Stage.Update : Update();
                    break;
                case Stage.Exit :
                    return nextState;
            }
            return this;
        }
    }
}