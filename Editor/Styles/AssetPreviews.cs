using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CG.CombatFramework.Editor
{
    internal static class AssetPreviews
    {
        private const double k_PollSeconds = 0.15;
        private const double k_GiveUpSeconds = 6.0;
        private const int k_UnityCache = 50;
        private const int k_MaxCache = 1024;
        private const int k_Spare = 16;

        private sealed class Waiter
        {
            public Object Asset;
            public Action<Texture2D> Ready;
            public double Since;
        }

        private static readonly Dictionary<Image, Waiter> waiting = new();
        private static readonly List<KeyValuePair<Image, Waiter>> pass = new();
        private static double nextPoll;
        private static bool hooked;
        private static int cacheSize = k_UnityCache;

        internal static int Waiting => waiting.Count;
        internal static int CacheSize => cacheSize;

        public static void Fit(int count)
        {
            int wanted = Mathf.Min(k_MaxCache, count + k_Spare);
            if(wanted <= cacheSize) return;
            cacheSize = wanted;
            AssetPreview.SetPreviewTextureCacheSize(wanted);
        }

        public static void Show(Image image, Object asset, Action<Texture2D> ready)
        {
            if(image == null) return;
            waiting.Remove(image);
            var texture = asset != null ? AssetPreview.GetAssetPreview(asset) : null;
            image.image = texture;
            ready?.Invoke(texture);
            if(texture != null || asset == null) return;
            waiting[image] = new Waiter { Asset = asset, Ready = ready, Since = EditorApplication.timeSinceStartup };
            Hook();
        }

        public static void Cancel(Image image)
        {
            if(image != null) waiting.Remove(image);
        }

        private static void Hook()
        {
            if(hooked) return;
            hooked = true;
            nextPoll = EditorApplication.timeSinceStartup + k_PollSeconds;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if(now < nextPoll) return;
            nextPoll = now + k_PollSeconds;

            pass.Clear();
            pass.AddRange(waiting);
            foreach(var pair in pass)
            {
                var image = pair.Key;
                var waiter = pair.Value;
                if(!waiting.TryGetValue(image, out var current) || current != waiter) continue;
                if(image.panel == null || waiter.Asset == null || now - waiter.Since > k_GiveUpSeconds)
                {
                    waiting.Remove(image);
                    continue;
                }
                var texture = AssetPreview.GetAssetPreview(waiter.Asset);
                if(texture == null) continue;
                waiting.Remove(image);
                image.image = texture;
                waiter.Ready?.Invoke(texture);
            }
            pass.Clear();

            if(waiting.Count > 0) return;
            EditorApplication.update -= Poll;
            hooked = false;
        }
    }
}
