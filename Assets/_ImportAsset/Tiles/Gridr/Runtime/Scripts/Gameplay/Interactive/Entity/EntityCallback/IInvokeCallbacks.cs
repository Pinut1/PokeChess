//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;

namespace Gridr.Gameplay
{
    public interface IInvokeCallbacks
    {
        
        ///<summary>
        /// Adds a callback to the queue of transient callbacks. Will be removed once called.
        ///</summary>
        public void AddTransient(Action callback);
        
        ///<summary>
        /// Adds a callback to the list of persistent callbacks. Well persist as long as the object does or list is manually cleared.
        ///</summary>
        public void AddPersistent(Action callback);
        
        ///<summary>
        /// Adds a callback to the list of persistent callbacks. Well persist as long as the object does or list is manually cleared.
        ///</summary>
        public void InvokeAll();
        
        public void InvokeTransient();
        public void InvokePersistent();
    }
}