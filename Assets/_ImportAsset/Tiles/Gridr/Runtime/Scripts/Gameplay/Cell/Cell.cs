//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Object representation of a grid cell.
    ///</summary>
    [Serializable]
    public class Cell : MonoBehaviour, IEquatable<Cell>, ISelectable
    {
        [Tooltip("The priority of this item when several selectable are detected")]
        [SerializeField] protected int selectionPriority = 9;
        [Tooltip("Shared data of a cell. Contains cost of traversal and directions for building the graph")]
        [SerializeField] protected CellData data;
        [Tooltip("Represents point of solid ground on a cell. Units standing on the cell will be adjusted to this transform")]
        [SerializeField] protected Transform groundPoint;
        [Tooltip("GridDisplay handles displaying reachable cells and paths to the player")]
        [SerializeField] protected GridDisplay gridDisplay;
        
        public GridPosition gridPosition;

        private List<GridEntity> _occupants = new List<GridEntity>();

        
        public string Name  => data.cellName;
        ///<summary>
        /// Directions represent neighbor nodes that will be connected in the graph, measured in units of cells.
        ///</summary>
        public Direction[] Directions => data.connections.directions;
        ///<summary>
        /// Weight of graph edge to the node. The cost of traversing to the node.
        ///</summary>
        public float Cost => data.cost;
        ///<summary>
        /// Determines if the cell is connected to other cells in the graph. If false it will not be traversable.
        ///</summary>
        public bool Connected => data.connected;
        ///<summary>
        /// The point that represents the ground of the cell. 
        ///</summary>
        public Transform GroundPoint  => groundPoint == null ? transform : groundPoint;

        ///<summary>
        /// The Grid Entities currently occupying the cell.
        ///</summary>
        public List<GridEntity> Occupants => _occupants.ToList();

        ///<summary>
        /// Determines whether the cell is occupied at this moment.
        ///</summary>
        public bool Occupied => _occupants != null && _occupants.Any();
        
        

        public virtual State Select() { return null; }
        public virtual void Deselect() { }
        public virtual void SetAsDefault()
        {
            if(gridDisplay)
                gridDisplay.DeactivateAll();
        }
        public virtual void RemoveOccupant(GridEntity entity) => _occupants.Remove(entity);
        public virtual void RemoveAllOccupants() => _occupants.Clear();
        public virtual void AddOccupant(GridEntity entity) => _occupants.Add(entity);
        public virtual void SetAsPath() { if (gridDisplay) gridDisplay.ActivatePath(); }
        public virtual void SetAsInRange() { if (gridDisplay) gridDisplay.ActivateInRange(); }
        public virtual void SetAsHighlighted() { if (gridDisplay) gridDisplay.ActivateHighlight(); }
        public virtual void DeactivateAllHighlight() { if (gridDisplay) gridDisplay.DeactivateAll(); }
        
        public bool Equals(Cell other)
        {
            return other != null && gridPosition == other.gridPosition;
        }

        public int GetPriority()
        {
            return selectionPriority;
        }
    }
}