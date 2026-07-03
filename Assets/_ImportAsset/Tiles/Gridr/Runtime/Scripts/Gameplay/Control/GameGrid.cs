//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Extensions;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Stores all information about the grid and builds the graph.
    ///</summary>
    public class GameGrid : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Determines which type of grid to build")]
        [SerializeField] public GridType gridType;
        [SerializeField] public ViewType viewType;

        [Header("Grid Properties")]
        public Vector3 origin;
        [Tooltip("size of the cell in units. Hexagons are measured by outer radius")]
        public float cellSize;
        public int width;
        public int height;
        
        
        [Header("Graph")]
        public Cell[] cells;
        
        public Graph<Cell> Graph { get; private set; }
        public int AmountOfNodes => Graph.Count;
        public int AmountOfEdges => Graph.EdgesCount;
        public int Size  => cells.Select(c => c.gridPosition.index).Max() + 1;
        public HashSet<GridEntity> Entities => _entitySet.set;

        private TurnManager _turnManager;
        private GridSet<GridEntity> _entitySet;

        
        public void SetUp(float cellSize, int width, int height, Vector3 origin,
            ViewType viewType = ViewType.View3D, GridType gridType = GridType.Square)
        {
            this.cellSize = cellSize;
            this.width = width;
            this.height = height;
            this.origin = origin;
            this.viewType = viewType;
            this.gridType = gridType;
        }

        
        private void OnDisable()
        {
            if(_entitySet == null)
                return;
            _entitySet.onAdded -= OnAddedToSet;
            _entitySet.onRemoved -= OnRemovedFromSet;
        }

        public void Init(TurnManager turnManager, GridSet<GridEntity> entities)
        {
            _turnManager = turnManager;
            _entitySet = entities;
            
            _entitySet.onAdded += OnAddedToSet;
            _entitySet.onRemoved += OnRemovedFromSet;

            foreach (var entity in _entitySet.set)
            {
                foreach (var action in entity.Actions)
                {
                    action.AddPersistent(RestoreGrid);
                }
            }
            
            BuildGameGrid(cells);
        }
        
        public void BuildGameGrid(Cell[] cells)
        {
            Graph = BuildGraph(cells);
        }
        
        private Graph<Cell> BuildGraph(Cell[] graphNodes)
        {
            var graph = new Graph<Cell>();
            cells = graphNodes;
            
            foreach (var node in graphNodes)
            {
                graph.AddNode(node, node.gridPosition.index);
            }

            foreach (var node in graph.Nodes)
            {
                if (!node.Value.Connected)
                    continue;

                foreach (var direction in node.Value.Directions)
                {
                    var neighborNode = graph.Nodes.FirstOrDefault(n =>
                        n.Value.gridPosition.position == (node.Value.gridPosition.position + direction.direction));
                    if (neighborNode == null)
                        continue;
                    if (!neighborNode.Value.Connected)
                        continue;

                    var edgeWeight = neighborNode.Value.Cost * direction.costMultiplier;
                    node.AddEdge(neighborNode, edgeWeight);
                }
            }

            return graph;
        }
        
        
        public void OnAddedToSet(GridEntity entity)
        {
            RestoreGrid();
        }
        
        public void OnRemovedFromSet(GridEntity entity)
        {
            RestoreGrid();
        }
        
        public void RestoreGrid()
        {
            SetAllCellsAsDefault();
            UpdateGrid();
        }
        
        private void UpdateGrid()
        {
            SetAllCellsAsVacant();
            if (_entitySet == null)
            { 
                FindObjectsByType<GridEntity>(FindObjectsSortMode.None).Where(entity => entity != null && entity.Cell != null).ForEach(entity => entity.Cell.AddOccupant(entity));
                Debug.LogWarning("entitySet has not been defined. Searching for GridEntities using FindObjectsOfType instead. This can be inefficient.");
                return;
            }
            _entitySet.set.Where(entity => entity != null && entity.Cell != null).ForEach(entity => entity.Cell.AddOccupant(entity));
        }

        private void SetAllCellsAsDefault() => cells.ForEach(cell => cell.SetAsDefault());
        private void SetAllCellsAsVacant() => cells.ForEach(cell => cell.RemoveAllOccupants());
        

        public static bool IsInRange(Cell cell, GridEntity entity, int range)
        {
            var pos = cell.gridPosition - entity.GridPosition;
            var xLess = Mathf.Abs(pos.position.x) <= range;
            var yLess = Mathf.Abs(pos.position.y) <= range;
            var xyLess = Mathf.Abs(pos.position.y) + MathF.Abs(pos.position.x) <= range;

            return xLess && yLess && xyLess;
        }

        public static IEnumerable<Cell> CellsInVision(IEnumerable<GridEntity> entities, GameGrid gameGrid)
        {
            foreach (var cell in gameGrid.cells)
            {
                foreach (var entity in entities)
                {
                    var vision = (VisionProperty)entity.FindProperty(typeof(VisionProperty));
                    if(vision == null)
                        continue;
                    
                    if (IsInRange(cell, entity, vision.range))
                        yield return cell;
                }
            }
        }
        
        public static IEnumerable<Cell> CellsOutOfVision(IEnumerable<GridEntity> entities, GameGrid gameGrid)
        {
            foreach (var cell in gameGrid.cells)
            {
                foreach (var entity in entities)
                {
                    var vision = (VisionProperty)entity.FindProperty(typeof(VisionProperty));
                    if(vision == null)
                        continue;
                    if (!IsInRange(cell, entity, vision.range))
                        yield return cell;
                }
            }
        }
    }
}