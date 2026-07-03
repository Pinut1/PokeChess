//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Extensions;
using Gridr.Gameplay.Events;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Base component of an interactive unit on the grid
    ///</summary>
    public class GridEntity : MonoBehaviour, ISelectable, IPropertyHolder
    {
        [Tooltip("The priority of this item when several selectable are detected")]
        [SerializeField] protected int selectionPriority = 1;
        
        [SerializeField] private TypeID id;

        [SerializeField] [Tooltip("Where the unit stands on the ground")]
        private Transform ground;

        [Header("Behaviour")] 
        [Tooltip("The actions that the GridEntity can perform")] [SerializeField]
        private TiedList<GridAction> actions;

        [SerializeField] [Tooltip("Properties")] 
        private List<GridProperty> properties;

        [Header("Events")] 
        [Tooltip("Raised in OnEnable callback. This is used to let the game know that a new GridEntity has been activated in the scene")] 
        public EntityGameEvent entityEnabled;
        [Tooltip("Raised in OnDisable callback. This is used to let the game know that a new GridEntity has been de-activated in the scene")] 
        public EntityGameEvent entityDisabled;

        [Header("Callbacks")] 
        [Tooltip("List of callbacks to be invoked when the calling Activate method")]
        public EntityCallbacks onActivateEntity;
        [Tooltip("List of callbacks to be invoked when the calling Deactivate method")]
        public EntityCallbacks onDeactivateEntity;


        public TypeID ID => id;
        ///<summary>
        /// Point where the Entity object meets the ground
        ///</summary>
        public Transform Ground => ground == null ? gameObject.transform : ground;
        ///<summary>
        /// The Offset between the Entity and Ground
        ///</summary>
        public Vector3 PositionOffset => (gameObject.transform.position - Ground.position);
        ///<summary>
        /// Flag to indicate if the GridEntity is active
        ///</summary>
        public bool Active { get; set; }
        ///<summary>
        /// returns the next action in the action list 
        ///</summary>
        public GridAction NextAction => GetNextAction();
        ///<summary>
        /// The position of the GridEntity as a Cell. Requires cell to be initialized 
        ///</summary>
        public Cell Cell => CalculateCell();
        ///<summary>
        /// The position of the GridEntity as a GridPosition. Requires cell to be initialized 
        ///</summary>
        public GridPosition GridPosition => _calculateGridPosition?.Invoke(gameObject.transform.position) ?? new GridPosition(-1, -1, -1, -1);
        ///<summary>
        /// All the actions of this GridEntity 
        ///</summary>
        public IEnumerable<GridAction> Actions => actions;

        private Func<Vector3, GridPosition> _calculateGridPosition;
        private Func<GridPosition, Cell> _calculateCell;
        private GameGrid _gameGrid;
        
        public GameGrid GameGrid => _gameGrid;
        private Queue<GridAction> _actionLoop = new Queue<GridAction>();

        private void OnEnable() => entityEnabled.Raise(this);
        private void OnDisable() => entityDisabled.Raise(this);
        public void Activate() => onActivateEntity.InvokeAll(this);
        public void Deactivate() => onDeactivateEntity.InvokeAll(this);


        public void Initialize(GameGrid gameGrid, Func<Vector3, GridPosition> calculateGridPosition,
            Func<GridPosition, Cell> calculateCell)
        {
            _calculateGridPosition = calculateGridPosition;
            _calculateCell = calculateCell;
            _gameGrid = gameGrid;
        }
        

        public State Select()
        {
            return !CanTakeAction() ? null : GetNextAction()?.Get();
        }

        public void Deselect()
        {
            RestartActions();
        }

        public void ActivateActions()
        {
            actions.ForEach(c => c.Activate());
        }
        
        private void RestartActions()
        {
            actions.Restart();
        }

        private GridAction GetNextAction(int count = 0)
        {
            if (!Active || count >= actions.Count)
                return null;

            var nextBehaviour = actions.Next();
            return nextBehaviour.Active ? nextBehaviour : GetNextAction(++count);
        }
        

        private bool CanTakeAction()
        {
            return Active && actions.Any(behaviour => behaviour.Active);
        }

        public GridPosition CalculateGridPosition(Vector3 position)
        {
            return _calculateGridPosition(position);
        }

        public Cell CalculateCell()
        {
            return _calculateCell?.Invoke(CalculateGridPosition(gameObject.transform.position));
        }

        ///<summary>
        /// Searches the properties list of the Entity
        ///</summary>
        public GridProperty FindProperty(Type type)
        {
            if (properties == null || !properties.Any())
                return null;

            return properties.FirstOrDefault(property => property.GetType() == type);
        }
        
        public IEnumerable<GridProperty> FindProperties(Type type) => properties.Where(property => property.GetType() == type);
        public IEnumerable<GridProperty> GetAll()
        {
            return properties;
        }


        ///<summary>
        /// Searches the actions list of the Entity
        ///</summary>
        public GridAction FindAction(Type type)
        {
            if (actions == null || !actions.Any())
                return null;

            return actions.FirstOrDefault(action => action.GetType() == type);
        }

        public int GetPriority()
        {
            return selectionPriority;
        }
    }
}