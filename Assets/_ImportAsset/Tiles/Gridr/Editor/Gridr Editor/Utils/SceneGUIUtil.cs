//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using Gridr.Datastructures;
using Gridr.Gameplay;
using ScriptableObjects.Editor;
using UnityEditor;
using UnityEngine;

namespace Gridr.Editor.Utils
{
    public class SceneGUIUtil
    {
        private int _currentBrushIndex = -1;
        private int _currentDrawTypeIndex = -1;
        private Brushes _previouslySelectedBrushes;

        public delegate void VoidDelegate();
        public delegate void SelectDrawTypeDelegate(int drawType);
        public delegate void SelectBrushDelegate(int index);

        private readonly List<(string text, VoidDelegate callback)> toolsButtons;
        private readonly List<(string text, VoidDelegate callback)> commandButtons;

        private static int BrushButtonsColsCount => GridBuilderSettings.Instance.brushColumns;
        private static float BrushesHStart => GridBuilderSettings.Instance.brushHorizontalStart;
        private static float BrushesVStart => GridBuilderSettings.Instance.brushVerticalStart;
        private static float BrushWidth => GridBuilderSettings.Instance.brushButtonWidth;
        private static float BrushHeight => GridBuilderSettings.Instance.brushButtonHeight;

        private static float CommandHStart => GridBuilderSettings.Instance.commandHorizontalStart;
        private static float CommandVStart => GridBuilderSettings.Instance.commandVerticalStart;
        private static float CommandHSpacing => GridBuilderSettings.Instance.commandHorizontalSpacing;
        private static float CommandVSpacing => GridBuilderSettings.Instance.commandVerticalSpacing;
        private static float CommandWidth => GridBuilderSettings.Instance.commandButtonWidth;
        private static float CommandHeight => GridBuilderSettings.Instance.commandButtonHeight;

        public static float ToolsHStart => GridBuilderSettings.Instance.horizontalStart;
        public static float ToolsVStart => GridBuilderSettings.Instance.verticalStart;
        public static float ToolsVSpacing => GridBuilderSettings.Instance.verticalSpacing;
        public static float ToolsHSpacing => GridBuilderSettings.Instance.horizontalSpacing;
        public static float ToolsWidth => GridBuilderSettings.Instance.textButtonWidth;
        public static float ToolsHeight => GridBuilderSettings.Instance.textButtonHeight;

        public static bool ShowEdges => GridBuilderSettings.Instance.showGraphEdges;
        public static bool ShowIndices => GridBuilderSettings.Instance.showGridPositionIndex;
        public static bool ShowPositions => GridBuilderSettings.Instance.showGridPosition;


        public SceneGUIUtil(List<(string text, VoidDelegate callback)> toolsButtons,
            List<(string text, VoidDelegate callback)> commandButtons)
        {
            this.toolsButtons = toolsButtons;
            this.commandButtons = commandButtons;
        }

        private void DrawBrushButton(int index, Brushes brushes, ref GameObject selectedBrush,
            SelectBrushDelegate onBrushSelected = null)
        {
            HandlePreviousBrush();

            var brush = brushes.brushes[index];

            if (brush == null)
            {
                Debug.LogWarning("Warning: Brush is null");
                return;
            }

            Texture2D tex;
            if (brushes.HasTextures)
            {
                tex = brushes.Textures4X[index];
                if (tex == null)
                    tex = AssetPreview.GetAssetPreview(brush);
            }
            else
                tex = AssetPreview.GetAssetPreview(brush);

            var row = index % BrushButtonsColsCount;
            var col = index / BrushButtonsColsCount;

            if (GUI.Button(GetBrushRect(), tex))
            {
                if (_currentBrushIndex == index)
                {
                    _currentBrushIndex = -1;
                    onBrushSelected?.Invoke(_currentBrushIndex);
                    selectedBrush = null;
                    return;
                }

                selectedBrush = brush;
                _currentBrushIndex = index;
                onBrushSelected?.Invoke(index);
            }

            void HandlePreviousBrush()
            {
                if (brushes != _previouslySelectedBrushes)
                {
                    _currentBrushIndex = -1;
                    onBrushSelected?.Invoke(_currentBrushIndex);
                    _previouslySelectedBrushes = brushes;
                }
            }
            Rect GetBrushRect()
            {
                return new Rect(
                    BrushesHStart + (row * BrushWidth),
                    BrushesVStart + (col * BrushHeight),
                    BrushWidth, BrushHeight);
            }
        }


        public void DrawBrushButtons(Brushes brushes, ref GameObject selectedBrush,
            SelectBrushDelegate onBrushSelected = null)
        {
            if (brushes == null)
                return;

            for (int i = 0; i < brushes.brushes.Length; i++)
            {
                if (i == _currentBrushIndex)
                    GUIUtil.SetColorToSelected();

                DrawBrushButton(i, brushes, ref selectedBrush, onBrushSelected);
                GUIUtil.SetColorToDefault();
            }
        }

        public void DrawDrawingButtons(SelectDrawTypeDelegate onSetDrawType)
        {
            for (int i = 0; i < commandButtons.Count; i++)
            {
                if (_currentDrawTypeIndex == i)
                    GUIUtil.SetColorToSelected();

                if (GUI.Button(new Rect(
                        CommandHStart + (i * CommandHSpacing),
                        CommandVStart + (i * CommandVSpacing),
                        CommandWidth, CommandHeight),
                    commandButtons[i].text))
                {
                    onSetDrawType?.Invoke(i);
                    _currentDrawTypeIndex = i;
                }

                GUIUtil.SetColorToDefault();
            }
        }

        public void DrawToolsButtons()
        {
            for (int i = 0; i < toolsButtons.Count; i++)
            {
                var rect = new Rect(ToolsHStart + (ToolsHSpacing * i), ToolsVStart + (ToolsVSpacing * i), ToolsWidth, ToolsHeight);

                if (GUI.Button(rect, toolsButtons[i].text, GUIUtil.GetStandardButtonStyle()))
                {
                    toolsButtons[i].callback?.Invoke();
                }
            }
        }

        public void DrawPositions(ViewType viewType, GameGrid grid)
        {
            if (grid == null)
                return;

            switch (viewType)
            {
                case ViewType.View3D:
                {
                    if (ShowPositions)
                        GridVisUtil.DrawPositions(grid.cells, GridBuilderSettings.Instance.graphEdgesOffset);
                    if (ShowIndices)
                        GridVisUtil.DrawIndices(grid.cells, GridBuilderSettings.Instance.graphEdgesOffset);
                    break;
                }
                case ViewType.View2D:
                {
                    if (ShowPositions)
                        GridVisUtil.DrawPositions(grid.cells, Vector3.forward);
                    if (ShowIndices)
                        GridVisUtil.DrawIndices(grid.cells, Vector3.forward);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}