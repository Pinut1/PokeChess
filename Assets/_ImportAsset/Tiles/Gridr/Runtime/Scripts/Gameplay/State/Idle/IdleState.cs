//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


using System.Linq;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Listens for user input 
    ///</summary>
    public class IdleState : State
    {
        private readonly IListener<ISelectable> _listener;
        private readonly IInputReader _inputReader;
        private readonly EntityCallbacks _onEnterIdle;
        
        public IdleState(IInputReader inputReader, IListener<ISelectable> listener, EntityCallbacks onEnterIdle)
        {
            _inputReader = inputReader;
            _listener = listener;
            _onEnterIdle = onEnterIdle;
        }

        protected override void Enter()
        {
            if(_onEnterIdle)
                _onEnterIdle.InvokeAll(null);
        }

        protected override void Update()
        {
            var selectedState = ListenForSelection(_inputReader.Primary());
            if (selectedState == null)
                return;

            nextState = selectedState;
            Exit();
        }
        
        State ListenForSelection(bool condition)
        {
            if(!condition)
                return null;
            
            var selected = _listener.ListenAll()?.OrderBy(selectable => selectable.GetPriority()).FirstOrDefault();
            
            return selected?.Select();
        }
    }
}