//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay
{
    public class WaitForCallbackState : State
    {
        public delegate State GetStateOnComplete();
        private readonly GetStateOnComplete _getNextState;

        public WaitForCallbackState(IInvokeCallbacks invoker, GetStateOnComplete getNextState)
        {
            invoker.AddTransient(Exit);
            _getNextState = getNextState;
        }
        
        protected override void Enter() { }
        protected override void Update() { }
        protected override void Exit()
        {
            nextState = _getNextState();
            base.Exit();
        }
    }
}