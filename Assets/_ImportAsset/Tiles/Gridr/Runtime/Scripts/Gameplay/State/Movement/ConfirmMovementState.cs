//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay
{
    ///<summary>
    /// Handles user input for the movement action
    ///</summary>
    public class ConfirmMovementState : State
    {
        private readonly MovementAction _movementAction;
        private readonly EntityCallbacks _onStartConfirmAction;
        
        private readonly IActionInputSettings _actionInputSettings;
        private readonly IInputReader _inputReader;
        private readonly IFilter<ISelectable> _selectionFilter;
        private readonly IFilter<ISelectable> _selectionFilterPointer;
        private readonly IHighlightCells _highLighter;
        
        private Cell _previousCell;

        public ConfirmMovementState(MovementAction movementAction, IInputReader inputReader, IListener<ISelectable> listener, IActionInputSettings actionInputSettings, EntityCallbacks onEnterConfirm)
        {
            _movementAction = movementAction;
            _inputReader = inputReader;
            _actionInputSettings = actionInputSettings;
            _selectionFilter = new SelectionFilter<ISelectable>(listener.Listen, listener.ListenAll,HandleNullSelected, HandleEntitySelected, HandleCellSelected);
            _selectionFilterPointer = new SelectionFilter<ISelectable>(listener.Listen, listener.ListenAll,HandlePointerAboveNull, HandlePointerAboveEntity, HandlePointerAboveCell);
            _onStartConfirmAction = onEnterConfirm;
            _highLighter = new DefaultHighlighter(movementAction.Entity.GameGrid);
        }

        protected override void Enter()
        {
            _movementAction.StartAction();
            
            if (_onStartConfirmAction)
                _onStartConfirmAction.InvokeAll(_movementAction.Entity, action: _movementAction);
        }

        protected override void Update()
        {
            _selectionFilter.Filter(_inputReader.Primary());
            _selectionFilterPointer.Filter();
        }
        
        private void HandleEntitySelected(GridEntity entity)
        {
            if (SelectedSelf())
            {
                HandleSelfSelected();
                Exit();
                return;
            }
            
            HandleNullSelected();
            Exit();
            
            bool SelectedSelf() => entity == _movementAction.Entity;
            void HandleSelfSelected() => nextState = _actionInputSettings.HandleSelfSelected(_movementAction.Entity.NextAction);
            //void HandleOtherSelected() => nextState = _actionInputSettings.HandleOtherSelected(_movementAction);
        }
        
        private void HandleCellSelected(Cell cell)
        {
            if (IsValidMove())
                HandleMovement();
            else
                HandleNullSelected();

            Exit();
            
            bool IsValidMove() => _movementAction.CanMove(cell);
            void HandleMovement()
            {
                _highLighter.DeactivateHighlighter();
                _movementAction.Move(cell);
                nextState = new WaitForCallbackState(_movementAction, GetNextState);

                State GetNextState() => _actionInputSettings.HandleNextState(_movementAction.Entity.NextAction);
            } 
        }

        private void HandleNullSelected()
        {
            _highLighter.DeactivateHighlighter();
            nextState = _actionInputSettings.HandleNullSelected(_movementAction);
            Exit();
        }

        private void HandlePointerAboveNull()
        {
            if(_previousCell == null)
                return;
            
            _highLighter.SetAsHighlighted(_movementAction.CanMove);
            _previousCell = null;
        }
        
        private void HandlePointerAboveCell(Cell cell)
        {
            if (cell == _previousCell)
                return;

            if (_movementAction.canAction[cell])
            {
                _highLighter.SetAsHighlighted(_movementAction.CanMove);
                _highLighter.SetAsPathHighlighted(_movementAction.CanMove, _movementAction.GetPath(cell));
            }
            else
            {
                _highLighter.SetAsHighlighted(_movementAction.CanMove);
            }
            
            _previousCell = cell;
        }
        
        private void HandlePointerAboveEntity(GridEntity entity)
        {
            if(_previousCell == null)
                return;
            
            _highLighter.SetAsHighlighted(_movementAction.CanMove);
            _previousCell = null;
        }
    }
}