//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gridr.Utils
{
    public enum Easing
    {
        Linear,
        InQuart,
        OutQuart,
        InOutQuard,
        InElastic,
        InCubic,
        OutCubic,
    }
    
    public static class TweenUtil
    {
        public const float TOLERANCE = 0.0001f;
        
        public static float Ease(float t, Easing easing)
        {
            switch (easing)
            {
                case Easing.Linear:
                    return t;
                case Easing.InOutQuard:
                    return EaseInOutQuart(t);
                case Easing.InElastic:
                    return EaseInElastic(t);
                case Easing.InCubic:
                    return EaseInCubic(t);
                case Easing.OutCubic:
                    return EaseOutCubic(t);
                case Easing.InQuart:
                    return EaseInQuart(t);
                case Easing.OutQuart :
                    return EaseOutQuart(t);
                default:
                    return t;
            }
        }

        public static float EaseInOutQuart(float t)
        {
            return t < 0.5f ? 8f * t * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 4f) / 2f;
        }

        public static float EaseInQuart(float t)
        {
            return t * t * t * t;
        }
        
        public static float EaseOutQuart(float t)
        {
            return 1 - (float)Math.Pow(1 - t, 4);
        }

        public static float EaseInElastic(float t)
        {
            float c4 = (float)(2 * Math.PI) / 3;
            return t == 0 ? 0 : Math.Abs(t - 1) < TOLERANCE ? 1 : (float)Math.Pow(2, -10 * t) * (float)Math.Sin((t * 10 - 0.75) * c4) + 1;
        }

        public static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        public static float EaseOutCubic(float t)
        {
            return 1 - (float)Math.Pow(1 - t, 3);
        }

        
        public static IEnumerator TweenTransform(Transform tweenObject, Vector3 endPos, float duration, Action onComplete = null, Easing easing = Easing.Linear)
        {
         
            Vector3 startPosition = tweenObject.position;
            float elapsedTime = 0;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                tweenObject.position = Vector3.Lerp(startPosition, endPos, Ease(t, easing));
                yield return null;
            }
            onComplete?.Invoke();
        }
        


        public static IEnumerator TweenFullDistance(Transform tweenObject, IEnumerable<Vector3> transforms, float duration, Action onComplete = null)
        {
            var transformsList = new List<Vector3>(transforms);
            if (transformsList.Count < 2)
            {
                onComplete?.Invoke();
                yield break; 
            }
            
            for (int i = 1; i < transformsList.Count; i++)
            {
                Vector3 startPosition = tweenObject.position;
                Vector3 endPosition = transformsList[i];
                float elapsedTime = 0;

                while (elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / duration;
                    tweenObject.position = Vector3.Lerp(startPosition, endPosition, t);
                    yield return null;
                }
            }

            onComplete?.Invoke();
        }

        public static IEnumerator TweenFullDistance(Transform tweenObject, IEnumerable<Transform> transforms, float duration, Action onComplete = null)
        {
            var transformsList = new List<Transform>(transforms);
            if (transformsList.Count < 2)
            {
                onComplete?.Invoke();
                yield break; 
            }
            
            for (int i = 1; i < transformsList.Count; i++)
            {
                Vector3 startPosition = tweenObject.position;
                Vector3 endPosition = transformsList[i].position;
                float elapsedTime = 0;

                while (elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = elapsedTime / duration;
                    tweenObject.position = Vector3.Lerp(startPosition, endPosition, t);
                    yield return null;
                }
            }

            onComplete?.Invoke();
        }

        public static IEnumerator FadeOutGroup(CanvasGroup canvasGroup, float timeInSeconds, int steps, Action onComplete = null)
        {
            float alpha = 1;
            canvasGroup.alpha = alpha;
            
            if (steps < 1)
                steps = 1;
            
            while (alpha > 0)
            {
                alpha -= timeInSeconds/steps;
                canvasGroup.alpha = alpha;
                yield return new WaitForSeconds(timeInSeconds/steps);
            }

            onComplete?.Invoke();
        }
        
        public static IEnumerator FadeInGroup(CanvasGroup canvasGroup, float timeInSeconds, int steps, Action onComplete = null)
        {
            float alpha = 0;
            canvasGroup.alpha = alpha;

            if (steps < 1)
                steps = 1;
            
            while (alpha < 1)
            {
                alpha += timeInSeconds / steps;
                canvasGroup.alpha = alpha;
                yield return new WaitForSeconds(timeInSeconds/steps);
            }
            
            onComplete?.Invoke();
        }
    }
}