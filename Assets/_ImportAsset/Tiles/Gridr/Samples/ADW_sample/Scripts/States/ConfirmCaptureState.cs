//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Linq;
using Gridr.Gameplay;
using Gridr.Utils;

namespace Gridr.Adw
{
    public class ConfirmCaptureState : State
    {
        private readonly CaptureAction _captureAction;
        private readonly EntityCallbacks _onStartCapture;
       
        private readonly IInputReader _inputReader;
        private readonly IActionInputSettings _actionInputSettings;
        private readonly IFilter<ISelectable> _selectionFilter;

        public ConfirmCaptureState(CaptureAction captureAction, IActionInputSettings actionInputSettings, IInputReader inputReader, IListener<ISelectable> listener, EntityCallbacks onStartCapture)
        {
            _captureAction = captureAction;
            _onStartCapture = onStartCapture;
            _actionInputSettings = actionInputSettings;
            _inputReader = inputReader;
            _selectionFilter = new SelectionFilter<ISelectable>(listener.Listen, listener.ListenAll, HandleNullSelected, HandleEntitySelected, HandleCellSelected, HandleButtonSelected);
        }
        
        protected override void Enter()
        {
            _captureAction.StartAction();
            
            if (_onStartCapture)
                _onStartCapture.InvokeAll(_captureAction.Entity, action: _captureAction);
            
            if (!_captureAction.canAction[_captureAction.Entity.Cell])
            {
                nextState = _actionInputSettings.HandleNextState(_captureAction.Entity.NextAction);
                Exit();
                return;
            }

            _captureAction.ActivateCaptureButton();
        }

        protected override void Update()
        {
            _selectionFilter.Filter(_inputReader.Primary());
        }

        private void HandleButtonSelected(GridButton button)
        {
            if ((button is CaptureButton))
            {
                var captureEntity = _captureAction.Entity.Cell.Occupants.FirstOrDefault(e => PropertyUtil.GetProperty<CaptureTargetProperty>(e) != null);
                if (captureEntity == null)
                {
                    Exit();
                    return;
                }
                
                
                nextState = new WaitForCallbackState(_captureAction, GetNextState);
                _captureAction.Capture(PropertyUtil.GetProperty<CaptureTargetProperty>(captureEntity));
                Exit();
                return;
            }
            
            HandleNullSelected();
            State GetNextState() => _actionInputSettings.HandleNextState(_captureAction.Entity.NextAction);
        }

        private void HandleEntitySelected(GridEntity entity)
        {
            if(SelectedSelf())
            {
                HandleSelfSelected();
                Exit();
                return;
            }
            HandleOtherSelected();
            Exit();
            
            bool SelectedSelf() => entity == _captureAction.Entity;
            void HandleOtherSelected() => nextState = _actionInputSettings.HandleOtherSelected(_captureAction);
            void HandleSelfSelected() => nextState = _actionInputSettings.HandleSelfSelected(_captureAction.Entity.NextAction);
        }
        
        private void HandleCellSelected(Cell cell)
        {
            HandleNullSelected();
        }
        
        private void HandleNullSelected()
        {
            nextState = _actionInputSettings.HandleNullSelected(_captureAction);
            Exit();
        }


        protected override void Exit()
        {
            _captureAction.DeactivateCaptureButton();
            _captureAction.Close();
            base.Exit();
        }

    }
}