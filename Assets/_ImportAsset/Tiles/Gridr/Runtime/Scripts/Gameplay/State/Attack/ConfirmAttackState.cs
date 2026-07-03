//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

namespace Gridr.Gameplay
{
    ///<summary>
    /// Handles user input for the attack action
    ///</summary>
    public class ConfirmAttackState : State
    {
        private readonly AttackAction _attackAction;
        private readonly EntityCallbacks _onStartConfirmAction;

        private readonly IInputReader _inputReader;
        private readonly IFilter<ISelectable> _selectionFilter;
        private readonly IActionInputSettings _actionInputSettings;
        private readonly IHighlightCells _highLighter;

        public ConfirmAttackState(AttackAction attackAction, IInputReader inputReader, IListener<ISelectable> listener,
            IActionInputSettings actionInputSettings, EntityCallbacks onStart)
        {
            _attackAction = attackAction;
            _inputReader = inputReader;
            _actionInputSettings = actionInputSettings;
            _highLighter = new DefaultHighlighter(attackAction.Entity.GameGrid);
            _selectionFilter = new SelectionFilter<ISelectable>(listener.Listen, listener.ListenAll, HandleNullSelected, HandleEntitySelected, HandleCellSelected);
            _onStartConfirmAction = onStart;
        }

        protected override void Enter()
        {
            _attackAction.StartAction();

            if (_onStartConfirmAction)
                _onStartConfirmAction.InvokeAll(_attackAction.Entity, action: _attackAction);
        }

        protected override void Update()
        {
            _selectionFilter.Filter(_inputReader.Primary());
        }

        private void HandleCellSelected(Cell cell)
        {
            HandleNullSelected();
        }

        private void HandleEntitySelected(GridEntity entity)
        {
            if (SelectedSelf())
            {
                HandleSelectedSelf();
                Exit();
                return;
            }

            var damageable = _attackAction.TryGetAttackTarget(entity);

            if (damageable != null)
                HandleActionSuccessful();
            else
                HandleNullSelected();

            Exit();

            bool SelectedSelf() => entity == _attackAction.Entity;
            void HandleSelectedSelf()
            {
                _highLighter.DeactivateHighlighter();
                nextState = _actionInputSettings.HandleSelfSelected(_attackAction.Entity.NextAction);
            }
            void HandleActionSuccessful()
            {
                _highLighter.DeactivateHighlighter();
                _attackAction.Attack(damageable);
                nextState = new WaitForCallbackState(_attackAction, GetNextState);

                State GetNextState() => _actionInputSettings.HandleNextState(_attackAction.Entity.NextAction);
            }
        }

        private void HandleNullSelected()
        {
            _highLighter.DeactivateHighlighter();
            nextState = _actionInputSettings.HandleNullSelected(_attackAction);
            Exit();
        }
    }
}