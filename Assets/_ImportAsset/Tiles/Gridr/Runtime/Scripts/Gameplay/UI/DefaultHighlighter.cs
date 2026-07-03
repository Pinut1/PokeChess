//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


using System;
using System.Collections.Generic;
using Gridr.Extensions;


namespace Gridr.Gameplay
{
    public class DefaultHighlighter : IHighlightCells
    {
        private GameGrid _gameGrid;

        public DefaultHighlighter(GameGrid gameGrid)
        {
            _gameGrid = gameGrid;
        }

        public void SetAsInRange(Cell cell)
        {
            cell.SetAsInRange();
        }
        
        public void SetAsHighlighted(Predicate<Cell> predicate)
        {
            foreach (var cell in _gameGrid.cells)
            {
                if(predicate(cell))
                    cell.SetAsHighlighted();
            }
        }

        public void SetAsPathHighlighted(Predicate<Cell> predicate, Stack<Cell> path)
        {
            foreach (var pathCell in path)
            {
                if(predicate(pathCell))
                    pathCell.SetAsPath();
            }
        }

        public void DeactivateHighlighter()
        {
            _gameGrid.cells.ForEach(cell => cell.DeactivateAllHighlight());
        }
        
    }
}