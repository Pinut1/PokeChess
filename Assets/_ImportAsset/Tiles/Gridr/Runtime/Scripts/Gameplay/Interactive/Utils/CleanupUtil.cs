//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Collections.Generic;
using UnityEngine;

namespace Gridr.Utils
{
    public static class CleanUpUtil
    {
        public static void DestroyContent<T>(ref List<T> items) where T : MonoBehaviour
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.Destroy(item.gameObject);
            }

            items.Clear();
        }

        public static void DestroyContent<T>(List<T> items) where T : MonoBehaviour
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.Destroy(item.gameObject);
            }
        }

        public static void DestroyContent<T>(ref T[] items) where T : MonoBehaviour
        {
            for (int i = items.Length - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.Destroy(item.gameObject);
            }
        }

        public static void DestroyContent(ref GameObject[] items)
        {
            for (int i = items.Length - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.Destroy(item);
            }
        }

        public static void DestroyContent(ref List<GameObject> items)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.Destroy(item);
            }
        }

        public static void DestroyContentImmediate<T>(ref List<T> items) where T : MonoBehaviour
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.DestroyImmediate(item.gameObject);
            }

            items.Clear();
        }

        public static void DestroyContentImmediate<T>(ref T[] items) where T : MonoBehaviour
        {
            for (int i = items.Length - 1; i >= 0; i--)
            {
                var item = items[i];
                Object.DestroyImmediate(item.gameObject);
            }
        }
    }
}