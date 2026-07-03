//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using System.Linq;
using Gridr.Datastructures;
using Gridr.Editor.Utils;
using Gridr.Gameplay;
using Gridr.Utils;
using ScriptableObjects.Editor;
using UnityEditor;
using UnityEngine;

namespace Gridr.Editor.Builder
{
    public class SquareBuilderEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Square Grid Builder")]
        public static void Open()
        {
            if (SceneView.sceneViews.Count == 0)
            {
                Debug.LogWarning("No open scenes views. GridBuilder cannot be used without a scene view open");
                return;
            }
            GetWindow<SquareBuilderEditorWindow>("Square Grid Builder");
        } 


        private bool DrawButton => Event.current.shift;
        public int Count => _instanceHandler.Count;
        private int CurrentBrush { get; set; } = -1;

        public GameGrid grid;

        public GameGrid Grid
        {
            get => grid;
            private set => grid = value;
        }

        private Graph<Cell> Graph
        {
            get => Grid == null ? null : Grid.Graph;
        }

        private bool BuildImmediate
        {
            get => SquareBuilderValues.Instance.buildImmediate;
        }

        private bool Is2D
        {
            get => ViewType == ViewType.View2D;
            set => ViewType = value == false ? ViewType.View3D : ViewType.View2D;
        }

        public Vector3 Origin
        {
            get => SquareBuilderValues.Instance.origin;
            set => SquareBuilderValues.Instance.origin = value;
        }

        public int Width
        {
            get => SquareBuilderValues.Instance.gridWidth;
            private set => SquareBuilderValues.Instance.gridWidth = value;
        }

        public int Height
        {
            get => SquareBuilderValues.Instance.gridHeight;
            private set => SquareBuilderValues.Instance.gridHeight = value;
        }

        public float CellSize
        {
            get => SquareBuilderValues.Instance.cellSize;
            private set => SquareBuilderValues.Instance.cellSize = value;
        }

        public bool AutoSize
        {
            get => SquareBuilderValues.Instance.autoSize;
            set => SquareBuilderValues.Instance.autoSize = value;
        }

        private Vector3 PointerPosition
        {
            get => Is2D ? EditorMouseUtil.GetMousePointOnPlane(Origin, Vector3.forward) : EditorMouseUtil.GetMousePointOnPlane(Origin, Vector3.up);
        }

        private bool IsPointerOnGrid
        {
            get => Is2D ? IsWithin2DGrid(PointerPosition - Origin) : IsWithin3DGrid(PointerPosition + Origin);
        }

        private Vector3 PointerCenter
        {
            get => Is2D ? SquareUtil2D.GetCellCenterFromWorldPos(PointerPosition, Origin, CellSize) : SquareUtil3D.GetCellCenterFromWorldPos(PointerPosition, Origin, CellSize);
        }

        private Brushes Brushes
        {
            get => SquareBuilderValues.Instance.brushes;
        }

        private Vector2Int PointerCell
        {
            get
            {
                if (!IsPointerOnGrid)
                    return new Vector2Int(-1, -1);

                return Is2D ? SquareUtil2D.GetCellFromWorldPos(PointerPosition, Origin, CellSize) : SquareUtil3D.GetCellFromWorldPos(PointerPosition, Origin, CellSize);
            }
        }

        public SerializedProperty PropGrid
        {
            get => SerializedInstance.FindProperty("grid");
        }

        private SerializedObject _so;

        private SerializedObject SerializedInstance
        {
            get { return _so ??= new SerializedObject(this); }
        }

        private DrawType DrawType { get; set; }
        public ViewType ViewType { get; private set; }
        private SelectedTab Tab { get; set; }

        private DrawSettingsUtil _settings;
        private DrawSquareBuilderValuesUtil _values;
        private SceneGUIUtil _sceneGUI;
        private InstanceHandler _instanceHandler;

        private void OnEnable()
        {
            Setup();
            SceneView.duringSceneGui += DuringSceneGUI;
            Undo.undoRedoPerformed += Refresh;
            Undo.undoRedoPerformed += Recreate;
            Undo.undoRedoPerformed += BuildGraph;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            Undo.undoRedoPerformed -= Refresh;
            Undo.undoRedoPerformed -= Recreate;
            Undo.undoRedoPerformed -= BuildGraph;
        }

        private void Setup()
        {
            var toolsButtons = new List<(string text, SceneGUIUtil.VoidDelegate callback)>()
            {
                ("Clear", Clear),
                ("Fill", Fill),
            };
            var drawingButtons = new List<(string text, SceneGUIUtil.VoidDelegate callback)>()
            {
                ("Draw", SetToDraw),
                ("Delete", SetToDelete)
            };

            _instanceHandler = new InstanceHandler();
            _settings = new DrawSettingsUtil();
            _values = new DrawSquareBuilderValuesUtil(this);
            _sceneGUI = new SceneGUIUtil(toolsButtons, drawingButtons);

            Tab = SelectedTab.Builder;

            LoadGameGrid();
            LoadSceneGrid();
            BuildGraph();
        }

        private void OnGUI()
        {
            SerializedInstance.Update();
            _values.Update();
            _settings.Update();

            switch (GUILayout.Toolbar((int) Tab, new string[] {"Creativity", "Comfort"}))
            {
                case (int) SelectedTab.Builder:
                    _values.DrawGridBuilder();
                    Tab = SelectedTab.Builder;
                    break;
                case (int) SelectedTab.Settings:
                    _settings.DrawSettingsOptions();
                    Tab = SelectedTab.Settings;
                    break;
            }

            HandleModifiedProperties();
        }

        void DuringSceneGUI(SceneView sceneView)
        {
            if (Event.current.type == EventType.MouseMove)
                sceneView.Repaint();

            Handles.BeginGUI();
            _sceneGUI.DrawBrushButtons(Brushes, ref _instanceHandler.activeObject, OnBrushSelected);

            _sceneGUI.DrawToolsButtons();
            _sceneGUI.DrawDrawingButtons(SetDrawType);
            _sceneGUI.DrawPositions(ViewType, grid);

            Handles.EndGUI();

            HandleGridInput();

            if (Event.current.type != EventType.Repaint)
                return;

            GridVisUtil.DrawSquareGrid(ViewType);
            GridVisUtil.DrawGraphEdges(Graph, ViewType);
        }

        private void HandleModifiedProperties()
        {
            if (SerializedInstance.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }

            if (_values.ApplyModifiedProperties())
            {
                FindOutOfRangeObjects();

                if (grid != null && SquareBuilderValues.Instance.moveWithOrigin)
                    grid.transform.position = Origin;
                LoadGridPositions();
                if (BuildImmediate)
                    BuildGraph();

                SceneView.RepaintAll();
            }

            if (_settings.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }
        }

        private void OnBrushSelected(int index)
        {
            CurrentBrush = index;

            if (!AutoSize)
                return;
            if (CurrentBrush == -1)
                return;

            var renderer = Brushes.brushes[index].GetComponent<Renderer>();
            if (renderer == null)
                return;

            CellSize = renderer.bounds.size.x;
        }


        private void HandleGridInput()
        {
            switch (DrawType)
            {
                case DrawType.Draw:
                    ListenToDraw();
                    break;
                case DrawType.Delete:
                    ListenToDelete();
                    break;
                default:
                    ListenToDraw();
                    break;
            }
        }

        private void FindOutOfRangeObjects()
        {
            var objectsOutsideOfGrid = _instanceHandler.allObjects.Where(o =>
            {
                Vector3 position1;
                return GetCell((position1 = o.transform.position)).x + 1 > Width || GetCell(position1).y + 1 > Height;
            });

            foreach (var gameObject in objectsOutsideOfGrid)
            {
                Undo.DestroyObjectImmediate(gameObject);
            }

            _instanceHandler.Refresh();
        }

        private void ListenToDelete()
        {
            DrawEmptyPreview();

            if (!DrawButton)
                return;
            if (!IsPointerOnGrid)
                return;

            var objectAtPosition = _instanceHandler.allObjects.FirstOrDefault(o => o.GetComponent<Cell>().gridPosition == GetGridPosition(PointerCell));

            if (objectAtPosition == null)
                return;

            Undo.DestroyObjectImmediate(objectAtPosition);
            Refresh();

            if (BuildImmediate)
                BuildGraph();
        }

        private void ListenToDraw()
        {
            GridVisUtil.SetColor(Color.green);
            if (CurrentBrush == -1 && IsPointerOnGrid)
                GridVisUtil.DrawSquareOnGrid3D(Get3DCell(PointerPosition), Origin, CellSize);
            GridVisUtil.SetColorToDefault();

            HandlePreview();

            if (!DrawButton || !IsPointerOnGrid)
                return;

            TryInstantiateAt(PointerCenter);

            if (BuildImmediate)
                BuildGraph();
        }


        private void HandlePreview()
        {
            GridVisUtil.SetDrawAlways();
            if (!Is2D)
            {
                Handle3DPreview();
            }
            else
            {
                Handle2DPreview();
            }
        }

        private void Handle2DPreview()
        {
            if (ExitClause())
            {
                _instanceHandler.SetAllObjectsToActive();
                return;
            }

            _instanceHandler.HandleHiddenObject(PointerCenter);

            var preview = Brushes.Textures2X[CurrentBrush];
            var middle = CellSize * .5f;
            Graphics.DrawTexture(new Rect(PointerCenter.x - middle, PointerCenter.y, CellSize, -CellSize), preview);

            bool ExitClause() => !Brushes.HasTextures || CurrentBrush == -1 || !IsPointerOnGrid || _instanceHandler.activeObject == null;
        }

        private void Handle3DPreview()
        {
            if (!IsPointerOnGrid || _instanceHandler.activeObject == null)
            {
                _instanceHandler.SetAllObjectsToActive();
                return;
            }

            _instanceHandler.HandleHiddenObject(PointerCenter);
            _instanceHandler.HandleHiddenObject(GetGridPosition(PointerCell));

            var pose = new Pose(PointerCenter, Quaternion.identity);
            Matrix4x4 poseMatrix = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            MeshFilter[] filters = _instanceHandler.activeObject.GetComponentsInChildren<MeshFilter>();
            foreach (var filter in filters)
            {
                Mesh mesh = filter.GetComponent<MeshFilter>().sharedMesh;
                Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;

                Matrix4x4 localMatrix = filter.transform.localToWorldMatrix;
                Matrix4x4 finalTransform = poseMatrix * localMatrix;

                mat.SetPass(0);
                Graphics.DrawMeshNow(mesh, finalTransform);
            }

            GridVisUtil.DrawSquareOnGrid3D(PointerCell, Origin, CellSize);
            GridVisUtil.SetColorToDefault();
        }


        private void DrawEmptyPreview()
        {
            GridVisUtil.SetDrawAlways();

            if (IsPointerOnGrid)
            {
                _instanceHandler.HandleHiddenObject(PointerCenter);
                GridVisUtil.DrawDottedSquareOnGrid3D(PointerCell, Origin, CellSize);
                GridVisUtil.SetColorToDefault();
            }
            else
            {
                _instanceHandler.SetAllObjectsToActive();
            }
        }

        private GameObject TryInstantiateAt(Vector3 worldPosition)
        {
            if (grid == null)
            {
                Debug.LogWarning("Warning: Drawing without a grid is not allowed");
                return null;
            }

            if (_instanceHandler.activeObject == null)
            {
                Debug.LogWarning("Warning: Please select a cell to enable Filling / Drawnig");
                return null;
            }

            var gridPosition = GetGridPosition(GetCell(worldPosition));

            BuilderUtil.CleanPosition(worldPosition, _instanceHandler);
            BuilderUtil.CleanGridPosition(gridPosition, _instanceHandler);

            
            var spawnObject = (GameObject) PrefabUtility.InstantiatePrefab(_instanceHandler.activeObject, grid.transform);

            Undo.RegisterCreatedObjectUndo(spawnObject, "Undo Paint");

            spawnObject.transform.position = worldPosition;
            var square = spawnObject.GetComponent<Cell>();
            if (square != null)
            {
                square.gridPosition = gridPosition;
                SceneUtil.RecordChanges(square);
            }

            _instanceHandler.AddToAllObjects(spawnObject);
            return spawnObject;
        }

        private void TryInstantiateAt(Vector2Int gridCell)
        {
            if (!IsPositionEmpty())
                return;

            var spawnObject = TryInstantiateAt(GetWorldPosFromCell(gridCell));
            if (spawnObject != null)
                _instanceHandler.AddToAllObjects(spawnObject);


            bool IsPositionEmpty() => _instanceHandler.allObjects.All(occupant => occupant.transform.position != GetWorldPosFromCell(gridCell));
        }


        private void Fill()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    TryInstantiateAt(new Vector2Int(x, y));
                }
            }

            if (BuildImmediate)
                BuildGraph();
        }

        private void Clear()
        {
            foreach (var gameObjectToDestroy in _instanceHandler.allObjects)
            {
                Undo.DestroyObjectImmediate(gameObjectToDestroy);
            }

            Refresh();
            LoadGridPositions();

            if (BuildImmediate)
                BuildGraph();
        }

        private void Recreate()
        {
            if (grid == null)
                return;
            
            var recreatedGameObjects = grid.GetComponentsInChildren<Cell>(true);
            foreach (var cell in recreatedGameObjects)
            {
                if (_instanceHandler.allObjects.Contains(cell.gameObject))
                    continue;

                _instanceHandler.AddToAllObjects(cell.gameObject);
            }

            _instanceHandler.SetAllObjectsToActive();

            LoadGridPositions();
            BuildGraph();
        }

        private void Refresh()
        {
            _instanceHandler.Refresh();
        }

        public void BuildGraph()
        {
            if (grid == null)
                return;

            grid.SetUp(CellSize, Width, Height, Origin, ViewType);
            grid.BuildGameGrid(LoadNodes());

            SceneUtil.RecordChanges(grid);
            SceneView.RepaintAll();

            Cell[] LoadNodes() => _instanceHandler.allObjects.Where(c => c.GetComponent<Cell>() != null).Select(c => c.GetComponent<Cell>()).ToArray();
        }

        private void LoadSquares()
        {
            ClearBeforeLoad();

            if (GetWithSquare().Any())
            {
                foreach (var cellInScene in GetWithSquare())
                {
                    _instanceHandler.allObjects.Add(cellInScene);
                }
            }

            void ClearBeforeLoad() => _instanceHandler.allObjects.Clear();
        }

        private void LoadCellSize()
        {
            var oneSquare = _instanceHandler.allObjects.FirstOrDefault(c => c.GetComponent<Cell>());
            if (oneSquare == null)
                return;

            var selectedRenderer = oneSquare.GetComponent<Renderer>();
            var meshBoundsSize = selectedRenderer.bounds.size;
            CellSize = meshBoundsSize.x;
        }

        public void LoadGridPositions()
        {
            foreach (var cell in _instanceHandler.allObjects)
            {
                var cellPos = Is2D ? Get2DCell(cell.transform.position) : Get3DCell(cell.transform.position);
                var gridPos = Is2D ? GetGridPosition2D(cellPos) : GetGridPosition3D(cellPos);

                var square = cell.GetComponent<Cell>();
                if (square == null)
                    continue;

                square.gridPosition = gridPos;
                SceneUtil.RecordChanges(square);
            }

            SceneView.RepaintAll();
        }

        private void LoadGameGrid()
        {
            var gameGrid = FindObjectOfType<GameGrid>();
            if (gameGrid != null)
                grid = gameGrid;
            else
            {
                Debug.LogWarning("No Game Grid detected, to use the grid builder, a Game Grid object needs to exist in the scene");
            }
        }

        private void LoadDimensions()
        {
            (Width, Height) = BuilderUtil.GetApproxWidthAndHeightSquare(_instanceHandler.allObjects);
        }

        private void LoadViewType()
        {
            ViewType = GuessViewType();
            GridVisUtil.SetSceneViewType(ViewType);
        }

        private ViewType GuessViewType()
        {
            var hasZ = _instanceHandler.allObjects.Select(gameObject => gameObject.GetComponent<Cell>()).Where(square => square != null).Count(square => square.transform.position.z > 0);

            return hasZ > 1 ? ViewType.View3D : ViewType.View2D;
        }

        public void LoadSceneGrid()
        {
            if (!IsValidSetup())
                return;

            LoadSquares();
            LoadViewType();
            LoadCellSize();
            LoadOrigin();
            LoadDimensions();
            LoadGridPositions();

            SceneView.RepaintAll();

            bool IsValidSetup() => FindObjectsOfType<Cell>().All(cell => cell != null);
        }


        public void LoadOrigin()
        {
            if (ViewType == ViewType.View2D)
            {
                var firstCell = _instanceHandler.allObjects.Select(c => c.GetComponent<Cell>()).FirstOrDefault(c => c.gridPosition.index == 0);
                if (firstCell != null)
                    Origin = firstCell.transform.position - new Vector3(CellSize * .5f, CellSize * .5f, 0);
            }

            if (ViewType == ViewType.View3D)
            {
                var firstCell = _instanceHandler.allObjects.Select(c => c.GetComponent<Cell>()).OrderBy(c => c.gridPosition.index).ToArray()[0];
                if (firstCell != null)
                    Origin = firstCell.transform.position - new Vector3(CellSize * .5f, 0, CellSize * .5f);
            }
        }


        public void SetTo2D()
        {
            GridVisUtil.SetSceneTo2D();
            ViewType = ViewType.View2D;
        }

        public void SetTo3D()
        {
            GridVisUtil.SetSceneTo3D();
            ViewType = ViewType.View3D;
        }

        void SetToDraw() => DrawType = DrawType.Draw;
        void SetToDelete() => DrawType = DrawType.Delete;
        void SetDrawType(int i) => DrawType = (DrawType) i;


        private Vector2Int GetCell(Vector3 worldPos) => Is2D ? Get2DCell(worldPos) : Get3DCell(worldPos);
        private Vector2Int Get2DCell(Vector3 worldPos) => SquareUtil2D.GetCellFromWorldPos(worldPos, Origin, CellSize);
        private Vector2Int Get3DCell(Vector3 worldPos) => SquareUtil3D.GetCellFromWorldPos(worldPos, Origin, CellSize);

        private bool IsWithin2DGrid(Vector3 worldPos) =>
            SquareUtil2D.IsWithinGrid(worldPos, Width, Height, CellSize);

        private bool IsWithin3DGrid(Vector3 worldPos) =>
            SquareUtil3D.IsWithinGrid(worldPos, Width, Height, CellSize);

        private Vector3 GetWorldPosFromCell(Vector2Int cell) => Is2D ? GetWorldPosFromCell2D(cell) : GetWorldPosFromCell3D(cell);

        private Vector3 GetWorldPosFromCell3D(Vector2Int cell) =>
            SquareUtil3D.GetWorldPosFromCellPos(cell.x, cell.y, Origin, CellSize);

        private Vector3 GetWorldPosFromCell2D(Vector2Int cell) =>
            SquareUtil2D.GetWorldPosFromCellPos(cell.x, cell.y, Origin, CellSize);

        private GridPosition GetGridPosition(Vector2Int cellPos) => Is2D ? GetGridPosition2D(cellPos) : GetGridPosition3D(cellPos);

        private GridPosition GetGridPosition2D(Vector2Int cellPos) =>
            new GridPosition(cellPos.x, cellPos.y, 0, SquareUtil2D.GetIndex(cellPos, Width));

        private GridPosition GetGridPosition3D(Vector2Int cellPos) =>
            new GridPosition(cellPos.x, cellPos.y, 0, SquareUtil3D.GetIndex(cellPos, Width));

        private IEnumerable<GameObject> GetWithSquare() =>
            FindObjectsOfType<GameObject>().Where(o => o.GetComponent<Cell>() != null);

        private int GetLargestIndex() => (Width * Height) - 1;
    }
}