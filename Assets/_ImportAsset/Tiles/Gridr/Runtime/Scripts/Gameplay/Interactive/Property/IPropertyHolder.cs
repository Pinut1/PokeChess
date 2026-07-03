//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;

namespace Gridr.Gameplay
{
    ///<summary>
    /// Enables searching for properties by type
    ///</summary>
    public interface IPropertyHolder
    {
        public GridProperty FindProperty(Type type);
        public IEnumerable<GridProperty>  FindProperties(Type type);
        public IEnumerable<GridProperty> GetAll();

    }
}