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
    public class HexGridBuilderEditorWindow : EditorWindow
    {
        [MenuItem("Tools/HexGridBuilder")]
        public static void Open()
        {
            if (SceneView.sceneViews.Count == 0)
            {
                Debug.LogWarning("No open scenes views. GridBuilder cannot be used without a scene view open");
                return;
            }
            GetWindow<HexGridBuilderEditorWindow>("Hex Grid Builder");
        }


        public int Count
        {
            get => _instanceHandler.allObjects.Count;
        }

        private bool DrawButtonTriggered
        {
            get => Event.current.shift;
        }

        public int CurrentBrush { get; set; } = -1;

        private Brushes Brushes
        {
            get => HexBuilderValues.Instance.brushes;
        }

        public GameGrid grid;

        public SerializedProperty PropGrid
        {
            get => SerializedInstance.FindProperty("grid");
        }

        public GameGrid Grid
        {
            get => grid;
            private set => grid = value;
        }

        private bool Is2D
        {
            get => ViewType == ViewType.View2D;
            set => ViewType = value == false ? ViewType.View3D : ViewType.View2D;
        }

        public Vector3 Origin
        {
            get => HexBuilderValues.Instance.origin;
            set => HexBuilderValues.Instance.origin = value;
        }

        public int Width
        {
            get => HexBuilderValues.Instance.gridWidth;
            private set => HexBuilderValues.Instance.gridWidth = value;
        }

        public int Height
        {
            get => HexBuilderValues.Instance.gridHeight;
            private set => HexBuilderValues.Instance.gridHeight = value;
        }

        public float OuterRadius
        {
            get => HexBuilderValues.Instance.cellSize;
            private set => HexBuilderValues.Instance.cellSize = value;
        }

        public float InnerRadius
        {
            get => HexUtil2D.GetInnerRadius(OuterRadius);
        }

        private Vector3 PointerPosition
        {
            get => Is2D
                ? EditorMouseUtil.GetMousePointOnPlane(Origin, Vector3.forward)
                : EditorMouseUtil.GetMousePointOnPlane(Origin, Vector3.up);
        }

        private Vector3Int PointerCubeCoords
        {
            get => Is2D
                ? HexUtil2D.CubeCoordsFromWorldPosition(Origin, PointerPosition, InnerRadius, OuterRadius)
                : HexUtil3D.CubeCoordsFromWorldPosition(Origin, PointerPosition, InnerRadius, OuterRadius);
        }

        private bool IsPointerOnGrid
        {
            get => Is2D
                ? HexUtil2D.IsWithinOffsetGrid(PointerCubeCoords, Width, Height)
                : HexUtil3D.IsWithinOffsetGrid(PointerCubeCoords, Width, Height);
        }

        private Vector3Int PointerOffsetCoords
        {
            get => Is2D
                ? HexUtil2D.CubeCoordsToOffset(PointerCubeCoords)
                : HexUtil3D.CubeCoordsToOffset(PointerCubeCoords);
        }

        private bool IsPointerWithinGrid
        {
            get => Is2D
                ? HexUtil2D.IsWithinOffsetGrid(PointerCubeCoords, Width, Height)
                : HexUtil3D.IsWithinOffsetGrid(PointerCubeCoords, Width, Height);
        }

        private Vector3 PointerCenter
        {
            get => Is2D
                ? HexUtil2D.CalculateHexCenterFromGridCoords(PointerCubeCoords.x, PointerCubeCoords.y, Origin,
                    InnerRadius, OuterRadius)
                : HexUtil3D.CalculateHexCenterFromGridCoords(PointerCubeCoords.x, PointerCubeCoords.y, Origin,
                    InnerRadius, OuterRadius);
        }


        public DrawType DrawType { get; set; }
        public ViewType ViewType { get; set; }
        public SelectedTab Tab { get; set; }


        private SerializedObject _so;

        public SerializedObject SerializedInstance
        {
            get { return _so ??= new SerializedObject(this); }
        }


        private DrawSettingsUtil _settings;
        private DrawHexBuilderValuesUtil _values;
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

        private void OnValidate()
        {
            BuildGraph();
        }

        private void Setup()
        {
            _settings = new DrawSettingsUtil();
            
            var toolsButtons = new List<(string, SceneGUIUtil.VoidDelegate)>()
            {
                ("Clear", Clear),
                ("Fill", Fill),
            };
            var drawingButtons = new List<(string text, SceneGUIUtil.VoidDelegate callback)>()
            {
                ("Draw", SetToDraw),
                ("Delete", SetToDelete)
            };

            _sceneGUI = new SceneGUIUtil(toolsButtons, drawingButtons);
            _values = new DrawHexBuilderValuesUtil(this);
            _instanceHandler = new InstanceHandler();

            Tab = SelectedTab.Builder;

            LoadGameGrid();
            LoadSceneGrid();
            BuildGraph();
        }

        private void OnGUI()
        {
            SerializedInstance.Update();
            _settings.Update();
            _values.Update();

            switch (GUILayout.Toolbar((int)Tab, new string[] { "Creativity", "Comfort" }))
            {
                case (int)SelectedTab.Builder:
                    _values.DrawGridBuilder();
                    Tab = SelectedTab.Builder;
                    break;
                case (int)SelectedTab.Settings:
                    _settings.DrawSettingsOptions();
                    Tab = SelectedTab.Settings;
                    break;
            }

            HandleModifiedProperties();
        }

        private void HandleModifiedProperties()
        {
            if (SerializedInstance.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }

            if (_values.ApplyModifiedProperties())
            {
                LoadGridPositions();
                BuildGraph();
                SceneView.RepaintAll();
            }

            if (_settings.ApplyModifiedProperties())
                SceneView.RepaintAll();
        }


        private void DuringSceneGUI(SceneView sceneview)
        {
            if (Event.current.type == EventType.MouseMove)
                sceneview.Repaint();

            Handles.BeginGUI();
            _sceneGUI.DrawBrushButtons(
                Brushes,
                ref _instanceHandler.activeObject,
                OnBrushSelected);
            _sceneGUI.DrawToolsButtons();
            _sceneGUI.DrawDrawingButtons(SetDrawType);
            _sceneGUI.DrawPositions(ViewType, grid);
            Handles.EndGUI();

            HandleGridInput();

            if (Event.current.type != EventType.Repaint)
                return;

            GridVisUtil.DrawHexGrid(ViewType, Origin);

            if (grid != null)
                GridVisUtil.DrawGraphEdges(grid.Graph, ViewType);
        }


        private void OnBrushSelected(int index)
        {
            CurrentBrush = index;
            if (HexBuilderValues.Instance.autoSize)
            {
                if (!Is2D)
                {
                    if (index < 0)
                        return;

                    if (HexBuilderValues.Instance.brushes.brushes.Length - 1 < index)
                        return;

                    var renderer = HexBuilderValues.Instance.brushes.brushes[index].GetComponent<Renderer>();
                    if (renderer == null)
                        return;

                    var size = renderer.bounds.size.z * .5f;
                    HexBuilderValues.Instance.cellSize = size;
                }

                if (Is2D)
                {
                    if (index < 0)
                        return;

                    if (HexBuilderValues.Instance.brushes.brushes.Length - 1 < index)
                        return;

                    var renderer = HexBuilderValues.Instance.brushes.brushes[index].GetComponent<Renderer>();
                    if (renderer == null)
                        return;

                    var size = renderer.bounds.size.y * .5f;
                    HexBuilderValues.Instance.cellSize = size;
                }
            }
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


        private bool IsPositionEmpty(Vector3Int gridCell)
        {
            foreach (var occupant in _instanceHandler.allObjects)
            {
                var cell = occupant.GetComponent<Cell>();
                if (cell == null)
                    continue;
                if (cell.gridPosition.position == gridCell)
                    return false;
            }

            return true;
        }

        private void Clear()
        {
            foreach (var gameObjectToDestroy in _instanceHandler.allObjects)
            {
                Undo.DestroyObjectImmediate(gameObjectToDestroy);
            }

            Refresh();
            LoadGridPositions();
            BuildGraph();
        }

        private void ListenToDraw()
        {
            GridVisUtil.DrawHex(PointerCubeCoords, Origin, OuterRadius, ViewType);
            HandleDrawing();
        }

        private void Fill()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 offset = CalculateHexCenterFromGridCoords(x, y);
                    var instance = TryInstantiateAtPosition(offset);
                    if (instance != null)
                        _instanceHandler.allObjects.Add(instance);
                }
            }

            BuildGraph();
        }

        private void ListenToDelete()
        {
            DrawEmptyPreview();

            if (!DrawButtonTriggered)
                return;


            if (IsPointerOnGrid)
            {
                Vector3 hexPosition;
                if (PointerCubeCoords.z % 2 == 0)
                    hexPosition =
                        HexUtil3D.GetWorldPositionEven(HexUtil3D.CubeCoordsToOffset(PointerCubeCoords) + Origin,
                            InnerRadius, OuterRadius);
                else
                    hexPosition =
                        HexUtil3D.GetWorldPositionOdd(HexUtil3D.CubeCoordsToOffset(PointerCubeCoords) + Origin,
                            InnerRadius, OuterRadius);

                var objectAtPosition =
                    _instanceHandler.allObjects.FirstOrDefault(c => c.transform.position == hexPosition);

                if (objectAtPosition == null)
                    return;

                Undo.DestroyObjectImmediate(objectAtPosition);
                Refresh();
            }

            BuildGraph();
        }

        private void Recreate()
        {
            if (grid == null)
                LoadGameGrid();

            var cells = grid.GetComponentsInChildren<Cell>(true);
            foreach (var cell in cells)
            {
                if (_instanceHandler.allObjects.Contains(cell.gameObject))
                    continue;
                _instanceHandler.AddToAllObjects(cell.gameObject);
            }

            _instanceHandler.SetAllObjectsToActive();
            LoadDimensions();
            LoadGridPositions();
            BuildGraph();
        }


        private void HandleDrawing()
        {
            HandlePreview();

            if (!DrawButtonTriggered)
                return;

            TryInstantiateAtPosition(PointerPosition);
            BuildGraph();
        }

        private void HandlePreview()
        {
            GridVisUtil.SetDrawAlways();

            if (Is2D)
            {
                Handle2DPreview(PointerCubeCoords);
            }
            else
            {
                Handle3DPreview(PointerCubeCoords);
            }
        }


        private void Refresh()
        {
            _instanceHandler.Refresh();
        }


        private void DrawEmptyPreview()
        {
            if (IsPointerOnGrid)
            {
                GridVisUtil.SetDrawAlways();

                var offsetCoords = HexUtil3D.CubeCoordsToOffset(PointerCubeCoords);
                var hexPosition = PointerCubeCoords.z % 2 == 0
                    ? GetHexPositionEven(offsetCoords)
                    : GetHexPositionOdd(offsetCoords);

                _instanceHandler.HandleHiddenObject(hexPosition);
                GridVisUtil.DrawHex3D(PointerCubeCoords, Origin, OuterRadius);
            }
            else
            {
                _instanceHandler.SetAllObjectsToActive();
            }
        }


        private void DrawDuplicates(List<(GameObject, GameObject)> duplicates)
        {
            Handles.color = Color.red;
            foreach (var tuple in duplicates)
            {
                Handles.DrawLine(tuple.Item1.transform.position, tuple.Item2.transform.position, 2f);
            }

            Handles.color = Color.white;
        }


        #region Spawning

        private void Handle3DPreview(Vector3Int cubeCoords)
        {
            if (!HexUtil3D.IsWithinOffsetGrid(cubeCoords, Width, Height) || _instanceHandler.activeObject == null)
            {
                _instanceHandler.SetAllObjectsToActive();
                return;
            }

            GridVisUtil.SetDrawAlways();

            var offsetCoords = HexUtil3D.CubeCoordsToOffset(cubeCoords);
            Vector3 hexPosition;


            if (cubeCoords.z % 2 == 0)
                hexPosition = HexUtil3D.GetWorldPositionEven(offsetCoords + Origin, InnerRadius, OuterRadius);
            else
                hexPosition = HexUtil3D.GetWorldPositionOdd(offsetCoords + Origin, InnerRadius, OuterRadius);

            if (_instanceHandler.activeObject == null)
                return;

            var safeCubeCoords = GetCubeCoords3D(hexPosition);

            Vector3Int GetCubeCoords3D(Vector3 worldPos) =>
                HexUtil3D.CubeCoordsFromWorldPosition(Origin, worldPos, InnerRadius, OuterRadius);

            var index = GetIndex3D(safeCubeCoords);
            int GetIndex3D(Vector3Int cubeCo) => HexUtil3D.GetIndex(cubeCo, Width, Height);

            var gridPosition = new GridPosition(safeCubeCoords, index);
            _instanceHandler.HandleHiddenObject(hexPosition);
            _instanceHandler.HandleHiddenObject(gridPosition);

            var pose = new Pose(hexPosition, Quaternion.identity);
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

            GridVisUtil.DrawHex3D(cubeCoords, Origin, OuterRadius);
            Handles.color = Color.white;
        }

        private void Handle2DPreview(Vector3Int cubeCoords)
        {
            if (!HexUtil2D.IsWithinOffsetGrid(cubeCoords, Width, Height) || _instanceHandler.activeObject == null)
            {
                _instanceHandler.SetAllObjectsToActive();
                return;
            }

            var offsetCoords = HexUtil2D.CubeCoordsToOffset(cubeCoords);
            var hexCenter = cubeCoords.z % 2 == 0 ? GetWorldPosEven2D(offsetCoords) : GetWorldPosOdd2D(offsetCoords);

            if (_instanceHandler.activeObject == null)
                return;

            _instanceHandler.HandleHiddenObject(hexCenter);

            var rect = new Rect(hexCenter.x - InnerRadius, hexCenter.y - OuterRadius, InnerRadius * 2, OuterRadius * 2);
            Graphics.DrawTexture(rect, Brushes.Textures[CurrentBrush]);
            Handles.color = Color.red;
            Handles.DrawSolidDisc(rect.center, Vector3.forward, .2f);
            Handles.DrawWireCube(new Vector3(rect.center.x, rect.center.y, 0.1f),
                new Vector3(rect.size.x, rect.size.y));
            Handles.color = Color.white;
        }


        private GameObject TryInstantiateAtPosition(Vector3 worldPosition)
        {
            var cubeCoords = GetCubeCoords(worldPosition);

            if (!IsWithinGrid())
                return null;
            if (!_instanceHandler.activeObject)
            {
                Debug.LogWarning("No Brush Selected");
                return null;
            }

            Vector3 hexPosition = GetHexPosition(cubeCoords);
            var safeCubeCoords = GetCubeCoords(hexPosition);
            var index = GetHexIndex(safeCubeCoords);
            var gridPosition = new GridPosition(safeCubeCoords, index);

            BuilderUtil.CleanPosition(hexPosition, _instanceHandler);
            BuilderUtil.CleanGridPosition(gridPosition, _instanceHandler);

            var spawnObject =
                (GameObject)PrefabUtility.InstantiatePrefab(_instanceHandler.activeObject, grid.transform);

            Undo.RegisterCreatedObjectUndo(spawnObject, "Undo Paint");

            spawnObject.transform.position = hexPosition;
            var hexagon = spawnObject.GetComponent<Cell>();
            if (hexagon != null)
            {
                hexagon.gridPosition = gridPosition;
                SceneUtil.RecordChanges(hexagon);
            }

            _instanceHandler.AddToAllObjects(spawnObject);
            return spawnObject;

            bool IsWithinGrid() =>
                    Is2D
                    ? HexUtil2D.IsWithinOffsetGrid(cubeCoords, Width, Height)
                    : HexUtil3D.IsWithinOffsetGrid(cubeCoords, Width, Height);
        }

        #endregion

  
        
        public void LoadGridPositions()
        {
            
            foreach (var cell in _instanceHandler.allObjects)
            {
                var hex = cell.GetComponent<Cell>();
                if (hex == null)
                    continue;
                
                var cubeCoords = GetCubeCoords(cell.transform.position);
                Vector3 hexPosition = GetHexPosition(cubeCoords);
                var safeCubeCoords = GetCubeCoords(hexPosition);
                var index = GetHexIndex(safeCubeCoords);
                var gridPosition = new GridPosition(safeCubeCoords, index);
                
                hex.gridPosition = gridPosition;
                
            }

            SceneView.RepaintAll();
      
        }

        private void LoadDimensions()
        {
            (Width, Height) = BuilderUtil.GetApproxWidthAndHeightHex(_instanceHandler.allObjects);
        }

        private ViewType DetermineViewType()
        {
            var hasZ = 0;
            foreach (var gameObject in _instanceHandler.allObjects)
            {
                var hexagon = gameObject.GetComponent<Cell>();
                if (hexagon != null)
                {
                    var selectedRenderer = hexagon.GetComponent<Renderer>();
                    if (selectedRenderer != null)
                        hasZ = selectedRenderer.bounds.size.z > .5 ? hasZ + 1 : hasZ;
                }
            }

            return hasZ > 1 ? ViewType.View3D : ViewType.View2D;
        }

        public void LoadSceneGrid()
        {
            if (!Validate())
                return;
            
            LoadHexagons();
            LoadViewType();
            LoadGridProperties();
            LoadGridPositions();
            LoadDimensions();
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
        
        private void LoadHexagons()
        {
            _instanceHandler.allObjects.Clear();
            foreach (var cellInScene in GetAlLCells())
            {
                _instanceHandler.allObjects.Add(cellInScene);

            }
        }

        private void LoadGridProperties()
        {
            var oneHexagon = _instanceHandler.allObjects.FirstOrDefault(c => c.GetComponent<Cell>() != null);
            if (oneHexagon == null)
                return;

            var selectedRenderer = oneHexagon.GetComponent<Renderer>();
            var meshBoundsSize = selectedRenderer.bounds.size;
            if (Is2D)
                OuterRadius = meshBoundsSize.y * .5f;
            else
                OuterRadius = meshBoundsSize.z * .5f;
        }

        private void LoadViewType()
        {
            ViewType = DetermineViewType();
            GridVisUtil.SetSceneViewType(ViewType);
        }


        private bool Validate()
        {
            return FindObjectsOfType<Cell>().All(cell => cell != null);
        }

        public void BuildGraph()
        {
            if (grid == null)
                return;

            var nodes = _instanceHandler.allObjects.Where(c => c.GetComponent<Cell>() != null)
                .Select(c => c.GetComponent<Cell>()).ToArray();


            grid.SetUp(OuterRadius, Width, Height, Origin, ViewType, GridType.Hexagonal);
            grid.BuildGameGrid(nodes);

            SceneUtil.RecordChanges(grid);
            SceneView.RepaintAll();
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
        
        public void SetToDraw() => DrawType = DrawType.Draw;
        public void SetToDelete() => DrawType = DrawType.Delete;
        public void SetDrawType(int i) => DrawType = (DrawType)i;
        
        private Vector3 GetWorldPosEven3D(Vector3Int offsetCoords) =>
            HexUtil3D.GetWorldPositionEven(offsetCoords + Origin, InnerRadius, OuterRadius);

        private Vector3 GetWorldPosOdd3D(Vector3Int offsetCoords) =>
            HexUtil3D.GetWorldPositionOdd(offsetCoords + Origin, InnerRadius, OuterRadius);

        private Vector3 GetWorldPosEven2D(Vector3Int offsetCoords) =>
            HexUtil2D.GetWorldPositionEven(offsetCoords + Origin, InnerRadius, OuterRadius);

        private Vector3 GetWorldPosOdd2D(Vector3Int offsetCoords) =>
            HexUtil2D.GetWorldPositionOdd(offsetCoords + Origin, InnerRadius, OuterRadius);

        Vector3 GetHexPositionEven(Vector3 offsetCoords) =>
            HexUtil3D.GetWorldPositionEven(offsetCoords + Origin, InnerRadius, OuterRadius);

        Vector3 GetHexPositionOdd(Vector3 offsetCoords) =>
            HexUtil3D.GetWorldPositionOdd(offsetCoords + Origin, InnerRadius, OuterRadius);

        Vector3 CalculateHexCenterFromGridCoords(int x, int y) =>
            HexUtil3D.CalculateHexCenterFromGridCoords(x, y, Origin, InnerRadius, OuterRadius);

        GameObject[] GetAlLCells() =>
            FindObjectsOfType<GameObject>().Where(c => c.GetComponent<Cell>() != null).ToArray();
        
              
        Vector3Int GetCubeCoords(Vector3 worldPos) => Is2D ? 
            HexUtil2D.CubeCoordsFromWorldPosition(Origin, worldPos, InnerRadius, OuterRadius) :
            HexUtil3D.CubeCoordsFromWorldPosition(Origin, worldPos, InnerRadius, OuterRadius);
        
        int GetHexIndex(Vector3Int cubeCoords) => Is2D ? HexUtil2D.GetIndex(cubeCoords, Width, Height) : HexUtil3D.GetIndex(cubeCoords, Width, Height);

        Vector3 GetHexPosition2D(Vector3Int cubeCoords) =>
            cubeCoords.z % 2 == 0 ? GetWorldPosEven2D(HexUtil2D.CubeCoordsToOffset(cubeCoords)) : GetWorldPosOdd2D(HexUtil2D.CubeCoordsToOffset(cubeCoords));

        Vector3 GetHexPosition3D(Vector3Int cubeCoords) =>
            cubeCoords.z % 2 == 0 ? GetWorldPosEven3D(HexUtil3D.CubeCoordsToOffset(cubeCoords)) : GetWorldPosOdd3D(HexUtil3D.CubeCoordsToOffset(cubeCoords));

        Vector3 GetHexPosition(Vector3Int cubeCoords) => Is2D ? GetHexPosition2D(cubeCoords) : GetHexPosition3D(cubeCoords);

    }
}