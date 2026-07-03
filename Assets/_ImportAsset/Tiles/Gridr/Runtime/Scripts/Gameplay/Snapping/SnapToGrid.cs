//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using Gridr.Datastructures;
using Gridr.Utils;
using UnityEngine;

namespace Gridr.Gameplay
{
    [ExecuteAlways]
    public class SnapToGrid : MonoBehaviour
    {
        [Header("Options")] [SerializeField] private bool disableDuringPlay;
        
        [Header("Grid properties")]
        [SerializeField] private GameGrid grid;
        [SerializeField] public Vector3 offset = new Vector3(0, 0, 0);

        private Vector3 origin = new Vector3(0, 0, 0);
        private float cellSize = 1;

        private Vector3 previousPosition;
        private Vector3 previousOffset;
        private GridEntity entity;

        private void OnEnable()
        {
            if(entity == null)
                entity = GetComponent<GridEntity>();
            if (grid == null)
                grid = FindFirstObjectByType<GameGrid>();
        }

        private void OnValidate()
        {
            if (grid == null)
                grid = FindFirstObjectByType<GameGrid>();

            if(grid == null)
                return;
            
            origin = grid.origin;
            cellSize = grid.cellSize;
            previousPosition = transform.position;
        }
        private void Update()
        {
            if(Application.isPlaying && disableDuringPlay)
                return;
            if (previousPosition == transform.position && previousOffset == offset)
                return;
            if(grid == null)
                return;
        
            switch (grid.gridType)
            {
                case GridType.Square:
                    HandleSquare();
                    previousPosition = transform.position;
                    previousOffset = offset;
                    break;
                case GridType.Hexagonal:
                    HandleHexagonal();
                    previousPosition = transform.position;
                    previousOffset = offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

    
        #region HandleSnapping
    
        private void HandleSquare()
        {
            switch (grid.viewType)
            {
                case ViewType.View2D:
                    SnapToSquare2D();
                    break;
                case ViewType.View3D:
                    SnapToSquare3D();
                    break;
            }
        }
        private void HandleHexagonal()
        {
            switch (grid.viewType)
            {
                case ViewType.View2D:
                    SnapToHex2D();
                    break;
                case ViewType.View3D:
                    SnapToHex3D();
                    break;
            }
        }
        private void SnapToHex3D()
        {
            var innerRadius = HexUtil3D.GetInnerRadius(cellSize);
            var cubeCoords = HexUtil3D.CubeCoordsFromWorldPosition(origin, transform.position, innerRadius, cellSize);
            var offsetCoords = HexUtil3D.CubeCoordsToOffset(cubeCoords);

            var hexPosition = cubeCoords.z % 2 == 0 ? GetHexPositionEven() : GetHexPositionOdd();


            SnapToCellCenter();

            Vector3 GetHexPositionEven() => HexUtil3D.GetWorldPositionEven(offsetCoords + origin, innerRadius, cellSize);
            Vector3 GetHexPositionOdd() => HexUtil3D.GetWorldPositionOdd(offsetCoords + origin, innerRadius, cellSize);
            void SnapToCellCenter() => transform.position = hexPosition + offset;
        }
        private void SnapToHex2D()
        {
            var innerRadius = HexUtil2D.GetInnerRadius(cellSize);
            var cubeCoords = HexUtil2D.CubeCoordsFromWorldPosition(origin, transform.position, innerRadius, cellSize);
            var offsetCoords = HexUtil2D.CubeCoordsToOffset(cubeCoords);

            var hexPosition = cubeCoords.z % 2 == 0 ? GetHexPositionEven() : GetHexPositionOdd();


            SnapToCellCenter();

            Vector3 GetHexPositionEven() => HexUtil2D.GetWorldPositionEven(offsetCoords + origin, innerRadius, cellSize);
            Vector3 GetHexPositionOdd() => HexUtil2D.GetWorldPositionOdd(offsetCoords + origin, innerRadius, cellSize);
            void SnapToCellCenter() => transform.position = hexPosition + offset;
        }
        private void SnapToSquare2D()
        {
            if (entity != null)
                SnapToCellCenterEntity();
            else
                SnapToCellCenter();

            void SnapToCellCenter() => transform.position = SquareUtil2D.GetCellCenterFromWorldPos(transform.position, origin + offset, cellSize);
            void SnapToCellCenterEntity() => transform.position = SquareUtil2D.GetCellCenterFromWorldPos(transform.position, origin + offset, cellSize) + entity.PositionOffset;
        }
        private void SnapToSquare3D()
        {
            if (entity != null)
                SnapToCellCenterEntity();
            else
                SnapToCellCenter();
            
            void SnapToCellCenter() => transform.position = SquareUtil3D.GetCellCenterFromWorldPos(transform.position, origin + offset, cellSize);
            void SnapToCellCenterEntity() => transform.position = SquareUtil3D.GetCellCenterFromWorldPos(transform.position, origin + offset, cellSize) + entity.PositionOffset;
        }
    
        #endregion
    }
}