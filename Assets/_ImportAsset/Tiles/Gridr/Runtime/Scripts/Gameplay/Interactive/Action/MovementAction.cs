//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using Gridr.Extensions;
using Gridr.Pathfinding;
using Scripts.Gameplay;
using UnityEngine;


namespace Gridr.Gameplay
{
    ///<summary>
    /// Stores data and methods for a movement action
    ///</summary>
    [RequireComponent(typeof(GridEntity))]
    public class MovementAction : GridAction
    {

        public MovementData data;
        public MovementSequence sequence;
        public MovementValidation validation;
        public InputModule inputModule;
        
        public readonly Dictionary<Cell, bool> canAction = new();
        public readonly Dictionary<Cell, float> costToCell = new();
        public readonly Dictionary<Cell, Cell> movementPaths = new();
        

        public override State Get()
        {
            return Active ? inputModule.Get(this) : null;
        }
        
        public override void StartAction()
        {
            UpdatePaths();
            UpdateAction();
            
            void UpdatePaths()
            {
                //Clear old values
                costToCell.Clear();
                movementPaths.Clear();
                
                //Calculate all paths and add to movementPaths
                var (distances, previous) = new Dijkstra<Cell>().GetPaths(Entity.GameGrid.Graph, Entity.GameGrid.Size, Entity.GridPosition.index);
                for (int i = 0; i < previous.Length; i++)
                {
                    var cell = Entity.GameGrid.Graph.Find(i)?.Value;
                    if (cell == null)
                        continue;
                    
                    movementPaths.Add(cell, Entity.GameGrid.Graph.Find(previous[i])?.Value);
                }
                
                //Update cost to each cell
                UpdateMovementCosts(distances);
            }
            
            void UpdateAction()
            {
                //Clear old values
                canAction.Clear();
                //Update actionable cells 
                Entity.GameGrid.cells.ForEach(cell => canAction.Add(cell, validation.Validate(cell, this)));
            }
            
            void UpdateMovementCosts(float[] distances)
            {
                for (int i = 0; i < distances.Length; i++)
                {
                    var cell = Entity.GameGrid.Graph.Find(i)?.Value;
                    if (cell == null)
                        continue;
                    
                    costToCell.Add(cell, distances[i]);
                }
            }
        }

        public Stack<Cell> GetPath(Cell cell)
        {
            var path = new Stack<Cell>();
            while (cell != null)
            {
                path.Push(cell);
                cell = movementPaths[cell];
            }
            return path;
        }
        
        public void Move(Cell endCell)
        {
            StartCoroutine(sequence.Run(Entity, GetPath(endCell), null, HandleActionTaken));
        }
        
        protected virtual void HandleActionTaken()
        {
            onActionTaken.InvokeAll(Entity, action: this, onCompleted: InvokeAll);
        }

        public bool CanMove(Cell cell)
        {
            return  cell != null && canAction[cell];
        }

        public override void Close() { }
    }
}