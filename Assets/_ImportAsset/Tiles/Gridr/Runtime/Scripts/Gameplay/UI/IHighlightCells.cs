//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;


namespace Gridr.Gameplay
{
    public interface IHighlightCells
    {
        public void SetAsHighlighted(Predicate<Cell> predicate);
        public void SetAsPathHighlighted(Predicate<Cell> predicate, Stack<Cell> path);
        public void DeactivateHighlighter();
    }
}