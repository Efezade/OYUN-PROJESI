using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TacticalRPG.Editor
{
    /// <summary>Use a fixed Full HD render target for the Editor Game view.</summary>
    public static class GameViewResolutionTool
    {
        public static void Configure()
        {
            if (Application.isBatchMode) return;
            try
            {
                var assembly = typeof(EditorWindow).Assembly;
                var viewType = assembly.GetType("UnityEditor.GameView");
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                if (viewType == null || sizesType == null || sizeType == null || kindType == null || groupType == null)
                    throw new InvalidOperationException("Unity GameView API bulunamadi");

                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var singleton = sizesType.BaseType.GetProperty("instance", flags)?.GetValue(null);
                var standalone = Enum.Parse(groupType, "Standalone");
                var group = sizesType.GetMethod("GetGroup", flags)?.Invoke(singleton, new[] { standalone });
                if (group == null) throw new InvalidOperationException("Standalone GameView boyutlari yok");

                var count = group.GetType().GetMethod("GetTotalCount", flags);
                var getSize = group.GetType().GetMethod("GetGameViewSize", flags);
                int index = -1;
                for (int i = 0; i < (int)count.Invoke(group, null); i++)
                {
                    var size = getSize.Invoke(group, new object[] { i });
                    int width = (int)sizeType.GetProperty("width", flags).GetValue(size);
                    int height = (int)sizeType.GetProperty("height", flags).GetValue(size);
                    if (width == 1920 && height == 1080) { index = i; break; }
                }
                if (index < 0)
                {
                    var fixedKind = Enum.Parse(kindType, "FixedResolution");
                    var newSize = Activator.CreateInstance(sizeType, flags, null,
                        new object[] { fixedKind, 1920, 1080, "Full HD 1920x1080" }, null);
                    group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { newSize });
                    index = (int)count.Invoke(group, null) - 1;
                }

                var view = Resources.FindObjectsOfTypeAll(viewType).OfType<EditorWindow>().FirstOrDefault()
                           ?? EditorWindow.GetWindow(viewType);
                viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, index);
                viewType.GetProperty("maximizeOnPlay", flags)?.SetValue(view, true);
                view.Repaint();
                Debug.Log($"[Goruntu] Editor GameView sabit 1920x1080, Play'de buyutme acik, secim {index}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Goruntu] GameView boyutu ayarlanamadi: {e.Message}");
            }
        }
    }
}
