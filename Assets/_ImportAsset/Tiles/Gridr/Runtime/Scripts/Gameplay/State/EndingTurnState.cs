//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Diagnostics;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Ends and starts a new turn in a set amount of time
    ///</summary>
    public class EndingTurnState : State
    {
        private readonly int _millisecondsDelay;
        private readonly Stopwatch _stopwatch;
        private readonly TurnManager _turnManager;

        public EndingTurnState(int millisecondsDelay,  TurnManager turnManager)
        {
            _millisecondsDelay = millisecondsDelay;
            _turnManager = turnManager;
            _stopwatch = new Stopwatch();
        }

        protected override void Enter()
        {
            _turnManager.EndTurn();
            _stopwatch.Start();
        }
        
        protected override void Update()
        {
            if(_stopwatch.Elapsed.Milliseconds < _millisecondsDelay)
                return;
            
            nextState = _turnManager.currentPlayer.PlayerStartTurnState;
            _turnManager.StartTurn();
            Exit();
        }
    }
}