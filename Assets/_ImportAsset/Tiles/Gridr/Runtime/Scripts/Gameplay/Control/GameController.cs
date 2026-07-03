//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Responsible for running the main loop of the game. 
    ///</summary>
    public class GameController : MonoBehaviour
    {
        [SerializeField] private EntityCallbacks onDefaultState; 
        
        private IListener<ISelectable> _selectionListener;
        private IInputReader _inputReader;
        private State _currentState;
        private bool _on;

        //Used whenever a return state is null
        private State DefaultState => new IdleState(_inputReader, _selectionListener, onDefaultState);
        
        public void Init(IInputReader inputReader, IListener<ISelectable> listener)
        {
            _selectionListener = listener;
            _inputReader = inputReader;
        }
        public void Pause() => _on = false;
        public void Resume() => _on = true;

        ///<summary>
        /// Use this method to start with a particular state
        ///</summary>
        public void Begin(State startState)
        {
            _currentState = startState;
            Resume();
        }
        
        public void GoToDefault()
        {
            _currentState = DefaultState;
        }
        
        ///<summary>
        /// Transitions between the game states
        ///</summary>
        void Update()
        { 
            if (_on)
                Progress();
        }
        
        ///<summary>
        /// Runs the current state until null is returned - at which point the default state will run.
        ///</summary>
        private void Progress()
        {
            _currentState ??= DefaultState;
            _currentState = _currentState.Run();
        }
    }
}