//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections.Generic;
using System.Linq;
using Gridr.Gameplay;


namespace Gridr.Utils
{
    ///<summary>
    /// Helps finding certain properties and conditions from GridProperties
    ///</summary>
    public static class PropertyUtil
    {

        public static T GetFirstPropertyOfType<T>(IPropertyHolder propertyHolder)
        {
            
            foreach (var gridProperty in propertyHolder.GetAll())
            {
                if (gridProperty is T tProperty)
                    return tProperty;
            }

            return default;
        }
        
        public static T GetProperty<T>(IPropertyHolder propertyHolder) where T: GridProperty
        {
            return propertyHolder?.FindProperty(typeof(T)) as T;
        }
        public static IEnumerable<T> GetProperties<T>(IPropertyHolder propertyHolder) where T: GridProperty
        {
            return propertyHolder?.FindProperties(typeof(T)).Cast<T>();
        }

        public static IEnumerable<T> GetProperties<T>(IPropertyHolder propertyHolder, Predicate<T> p) where T : GridProperty
        {
            foreach (var property in GetProperties<T>(propertyHolder))
            {
                if (p(property))
                    yield return property;
            }
        }
        
        public static IEnumerable<T> GetAllProperties<T>(IEnumerable<IPropertyHolder> propertyHolders) where T : GridProperty
        {
            foreach (var propertyHolder in propertyHolders)
            {
                if (GetProperty<T>(propertyHolder) != null)
                    yield return GetProperty<T>(propertyHolder);
            }
        }
        
        public static T GetFirstProperty<T>(IEnumerable<IPropertyHolder> propertyHolders) where T : GridProperty
        {
            foreach (var propertyHolder in propertyHolders)
            {
                if (GetProperty<T>(propertyHolder) != null)
                    return GetProperty<T>(propertyHolder);
            }

            return null;
        }
        public static T GetProperty<T>(IPropertyHolder propertyHolder, Predicate<T> p) where T : GridProperty
        {
            var property = GetProperty<T>(propertyHolder);
            if (p(property))
                return property;
           
            return null;
        }

        public static bool HasProperty<T>(IPropertyHolder propertyHolder) where T : GridProperty
        {
            return GetProperty<T>(propertyHolder) != null;
        }

        public static bool HasPropertyOfType<T>(IPropertyHolder propertyHolder)
        {
            foreach (var gridProperty in propertyHolder.GetAll())
            {
                if (gridProperty is T otherProperty)
                    return true;
            }

            return false;
        }
        
        public static bool IsSameTeam(IPropertyHolder propertyHolder1, IPropertyHolder propertyHolder2)
        {
            if (propertyHolder1 == null || propertyHolder2 == null)
                return false;
            
            var team1 = (propertyHolder1.FindProperty(typeof(GridTeamProperty)) as GridTeamProperty);
            var team2 = (propertyHolder2.FindProperty(typeof(GridTeamProperty)) as GridTeamProperty);

            return team1 != null && team2 != null && team1.team == team2.team;
        }
    }
}