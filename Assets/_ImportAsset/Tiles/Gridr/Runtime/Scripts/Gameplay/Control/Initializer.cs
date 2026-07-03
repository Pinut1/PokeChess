//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using Gridr.Datastructures;
using Gridr.Extensions;
using Gridr.Utils;
using UnityEditor;
using UnityEngine;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Responsible for injecting dependencies and initializing controllers.
    ///</summary>
    public class Initializer : MonoBehaviour
    {
      
        [SerializeField] private Player[] players;
        
        [Header("Selection")] 
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private LayerMask selectionMask;

        [SerializeField] private GameGrid grid;
        [SerializeField] private GameController gameController;
        [SerializeField] private TurnManager turnManager;
        
        private Vector3 _origin;
        private float _cellSize;
        private int _width;
        private int _height;

        private IInputReader _inputReader;
        private IListener<ISelectable> _listener;
        private IActionInputSettings _inputSettings;

        private GridSet<GridEntity> _entitySet = new();

        private void Awake()
        {
            Initialize();
        }
        
        //Start game i after all OnEnable callbacks :)
        private void Start()
        {
            StartGame();
        }

        
        private void Initialize()
        {
            _entitySet.Clear();
            
            grid.Init(turnManager, _entitySet);
            _origin = grid.origin;
            _cellSize = grid.cellSize;
            _width = grid.width;
            _height = grid.height;

            //Dependencies initialize first
            InitInputReader();
            InitSelectableListener();
            InitInputSettings();
            
            //Initialize Game Entities
            InitEntities();
            InitPlayers();
            InitAllInputModules(); 
            turnManager.Init(players);
        }



        ///<summary>
        /// Boots up the game
        ///</summary>
        private void StartGame()
        {
            grid.RestoreGrid();
            gameController.Init(_inputReader, _listener);

            if (turnManager.currentPlayer)
                gameController.Begin(turnManager.currentPlayer.PlayerStartTurnState);
            else
                gameController.Resume();
            
            turnManager.StartTurn();
        }

        private void InitPlayers()
        {
            foreach (var player in players)
            {
                player.Init(_entitySet);
            }
        }
        
        private void InitEntities()
        {
            InitEntities(GetEntities());
        }

        private IEnumerable<GridEntity> GetEntities() => FindObjectsByType<GridEntity>(FindObjectsSortMode.None);
        

        ///<summary>
        /// Creates the input reader to be used during the game
        ///</summary>
        private void InitInputReader()
        {
            _inputReader = grid.viewType switch
            {
                ViewType.View2D => new MouseInputReader(sceneCamera, Vector3.forward),
                ViewType.View3D => new MouseInputReader(sceneCamera, Vector3.up),
                _ => _inputReader
            };
        }

        ///<summary>
        /// Creates the input settings top be used during the game
        ///</summary>
        private void InitInputSettings()
        {
            _inputSettings = new MobileFriendlyInputSettings();
        }


        ///<summary>
        /// Creates a listener and fallback listener depending on grid type
        ///</summary>
        private void InitSelectableListener()
        {
            _listener = grid.viewType switch
            {
                ViewType.View2D => new RaycastListener2D<ISelectable>(
                    sceneCamera, 
                    selectionMask,
                    _inputReader,
                    new PlaneListener(Vector3.zero, Vector3.forward, sceneCamera, _inputReader),
                    (position) => ResolvePositionToSelectable(position, grid.gridType),
                    (position) => ResolvePositionToSelectables(position, grid.gridType)),
                ViewType.View3D => new RaycastListener3D<ISelectable>(sceneCamera, selectionMask, _inputReader),
                _ => _listener
            };
        }
        

        ///<summary>
        /// Initializes necessary parameters and methods for entities
        ///</summary>
        private void InitEntity(GridEntity entity)
        {
            switch (grid.gridType)
            {
                case GridType.Square:
                    InitEntityForSquare(entity);
                    _entitySet.Add(entity);

                    return;
                case GridType.Hexagonal:
                    InitEntityForHex(entity);
                    _entitySet.Add(entity);
                    
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void InitEntities(IEnumerable<GridEntity> entities)
        {
            entities.ForEach(InitEntity);
        }
        
        public void InitAllInputModules()
        {
            
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:InputModule");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                InputModule module = AssetDatabase.LoadAssetAtPath<InputModule>(assetPath);

                if (module != null)
                {
                    Debug.Log("Initializing: " + module);
                    module.Initialize(_inputReader, _listener, _inputSettings);
                }
            }
#endif
#if !UNITY_EDITOR
            // Load all InputModule assets from the Resources/InputModules folder
            InputModule[] modules = Resources.LoadAll<InputModule>("InputModule");

            foreach (InputModule module in modules)
            {
                if (module != null)
                {
                    Debug.Log("Initializing: " + module);
                    module.Initialize(_inputReader, _listener, _inputSettings);
                }
            }
#endif
        }

        public void OnEntityEnabled(GridEntity entity)
        {
            InitEntity(entity);
        }

        public void OnEntityDisabled(GridEntity entity)
        {
            _entitySet.Remove(entity);   
        }
        
        
        void InitEntityForHex(GridEntity entity)
        {

            if (grid.viewType == ViewType.View3D)
            {
                entity.Initialize(grid ,(pos) =>
                {
                    var cubeCoords = GetCubeCoords3D(pos);
                    return new GridPosition(cubeCoords, GetHexIndex3D(cubeCoords));
                }, GetCellStrategy);            
            }
            
            if (grid.viewType == ViewType.View2D)
            {
                entity.Initialize(grid ,(pos) =>
                {
                    var cubeCoords = GetCubeCoords2D(pos);
                    return new GridPosition(cubeCoords, GetHexIndex2D(cubeCoords));
                }, GetCellStrategy);   
            }
            
   
            Vector3Int GetCubeCoords3D(Vector3 pos) => HexUtil3D.CubeCoordsFromWorldPosition(_origin, pos, HexUtil3D.GetInnerRadius(_cellSize), _cellSize);
            int GetHexIndex3D(Vector3Int cubeCoords) => HexUtil3D.GetIndex(cubeCoords, _width, _height);
            
            Vector3Int GetCubeCoords2D(Vector3 pos) => HexUtil2D.CubeCoordsFromWorldPosition(_origin, pos, HexUtil2D.GetInnerRadius(_cellSize), _cellSize);
            int GetHexIndex2D(Vector3Int cubeCoords) => HexUtil2D.GetIndex(cubeCoords, _width, _height);
            
            Cell GetCellStrategy(GridPosition gridPosition) => grid.Graph.Find(gridPosition.index)?.Value;
        }

        void InitEntityForSquare(GridEntity entity)
        {
            if (grid.viewType == ViewType.View3D)
                entity.Initialize(grid,(pos) => SquareUtil3D.GetGridPosition(pos, _origin, _cellSize, _width), (gridPos) => grid.Graph.Find(gridPos.index)?.Value);
            if (grid.viewType == ViewType.View2D)
                entity.Initialize(grid,(pos) => SquareUtil2D.GetGridPosition(pos, _origin, _cellSize, _width), (gridPos) => grid.Graph.Find(gridPos.index)?.Value);
        }


        private Cell ResolvePositionToCell(Vector3 position, GridType gridType)
        {
            switch (gridType)
            {
                case GridType.Square:
                {
                    var gridPos = SquareUtil2D.GetGridPosition(position, _origin, _cellSize, _width);
                    var cell = grid.Graph.Find(gridPos.index);

                    return cell?.Value != null ? cell.Value : null;
                }
                case GridType.Hexagonal:
                {
                    var gridPos = HexUtil2D.GetGridPosition(_origin, position, HexUtil2D.GetInnerRadius(_cellSize), _cellSize, _width, _height);
                    var cell = grid.Graph.Find(gridPos.index);

                    return cell?.Value != null ? cell.Value : null;
                }
                default:
                    return null;
            }
        }
        
        private ISelectable ResolvePositionToSelectable(Vector3 position, GridType gridType)
        {
            switch (gridType)
            {
                case GridType.Square:
                {
                    var gridPos = SquareUtil2D.GetGridPosition(position, _origin, _cellSize, _width);
                    var cell = grid.Graph.Find(gridPos.index);

                    if (cell?.Value is ISelectable selectable)
                    {
                        return selectable;
                    }

                    return null;
                }
                case GridType.Hexagonal:
                {
                    var gridPos = HexUtil2D.GetGridPosition(_origin, position, HexUtil2D.GetInnerRadius(_cellSize), _cellSize, _width, _height);
                    var cell = grid.Graph.Find(gridPos.index);

                    if (cell?.Value is ISelectable selectable)
                    {
                        return selectable;
                    }

                    return null;
                }
                default:
                    return null;
            }
        }
        private IEnumerable<ISelectable> ResolvePositionToSelectables(Vector3 position, GridType gridType)
        {
            switch (gridType)
            {
                case GridType.Square:
                {
                    var gridPos = SquareUtil2D.GetGridPosition(position, _origin, _cellSize, _width);
                    var cell = grid.Graph.Find(gridPos.index);
                    
                    if (cell != null && cell.Value != null && cell.Value.Occupied)
                    {
                        foreach (GridEntity entity in cell.Value.Occupants)
                        {
                            yield return entity;
                        }
                    }

                    if (cell != null && cell.Value != null)
                    {
                        yield return cell.Value;
                    }
                    break;
                }
                case GridType.Hexagonal:
                {
                    var gridPos = HexUtil2D.GetGridPosition(_origin, position, HexUtil2D.GetInnerRadius(_cellSize), _cellSize, _width, _height);
                    var cell = grid.Graph.Find(gridPos.index);

                    foreach (GridEntity entity in cell.Value.Occupants)
                    {
                        yield return entity;
                    }

                    yield return cell.Value;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(gridType), gridType, null);
            }
        }
    }
}