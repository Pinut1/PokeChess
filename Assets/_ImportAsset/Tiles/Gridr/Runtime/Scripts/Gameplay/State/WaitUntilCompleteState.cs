//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


namespace Gridr.Gameplay
{
    ///<summary>
    /// Puts the game in a wait state until a condition is true
    ///</summary>
    public class WaitUntilCompleteState : State
    {
        public delegate bool IsComplete();
        public delegate State GetStateOnComplete();

        private readonly IsComplete _isComplete;
        private readonly GetStateOnComplete _getNextState;
        
        public WaitUntilCompleteState(IsComplete isComplete, GetStateOnComplete getNextState)
        {
            _isComplete = isComplete;
            _getNextState = getNextState;
        }

        protected override void Update()
        {
            if (_isComplete.Invoke())
            {
                Exit();
            }
        }

        protected override void Exit()
        {
            this.nextState = _getNextState();
            base.Exit();
        }
    }
}