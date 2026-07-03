//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social

using System.Threading.Tasks;
using Gridr.Datastructures;
using UnityEditor;
using UnityEngine;

namespace Gridr.Editor.Utils
{
    public static class GUIVisUtil
    {
        public delegate void VoidDelegate();
        public delegate Task TaskDelegate();


        public static void DrawHeader()
        {
            GUILayout.Space(10);
            DrawLabelCentered("Grid Builder", 0);
            GUILayout.Space(10);
        }

        public static void DrawInformation(string text)
        {
            GUILayout.Space(10);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(text);
            }
            GUILayout.Space(10);
        }

        public static void DrawGridProperties(SerializedProperty[] properties)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Properties:");
                EditorGUILayout.PropertyField(properties[0]);
                GUILayout.Space(5);
                EditorGUILayout.PropertyField(properties[1]);
                GUILayout.Space(5);

                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(properties[2], GUIUtil.GetWidthFieldContent());
                    GUILayout.Space(5);
                    EditorGUILayout.PropertyField(properties[3], GUIUtil.GetHeightFieldContent());
                    GUILayout.Space(5);
                }
            }

            GUILayout.Space(10);
        }
        
        public static void DrawProperty(string header ,SerializedProperty property)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(header);
                EditorGUILayout.PropertyField(property);
            }

            GUILayout.Space(10);
        }

        public static void DrawStyleOptions(ViewType viewType, VoidDelegate setTo2D, VoidDelegate setTo3D, SerializedProperty[] properties, GridType gridType = GridType.Square)
        {
            using (new GUILayout.HorizontalScope())
            {
                DrawGridIcon(gridType, viewType, 60);
                DrawTwoToggleButtonsV(viewType, ("3D", "2D"), (setTo3D, setTo2D), 60);

                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(60)))
                {
                    foreach (var prop in properties)
                    {
                        EditorGUILayout.PropertyField(prop);
                    }
                }
            }
        }

        public static void DrawInfo((string text, float value)[] topics, float width)
        {
            GUILayout.Label("Info: ");

            for (int t = 0; t < topics.Length; t += 2)
            {
                using (new GUILayout.HorizontalScope())
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (t + i >= topics.Length)
                            continue;
                        GUILayout.Label($"{topics[t + i].text}: {topics[t + i].value}", EditorStyles.helpBox, GUILayout.Width(width));
                    }
                }
            }
        }

        public static void DrawButton(string text, VoidDelegate callback, int marginTop, int marginBottom)
        {
            GUILayout.Space(marginTop);
            DrawOneButton(false, text, callback);
            GUILayout.Space(marginBottom);
        }

        public static void DrawGraphOptions(SerializedProperty propGraph,bool hideCondition, bool disabledScope, VoidDelegate buildCallback, SerializedProperty[] extras = null)
        {
            if(hideCondition)
                return;
            
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Box(GUIUtil.GetGridIsBuildingTexture(), EditorStyles.helpBox, GUILayout.Width(60), GUILayout.Height(60));
                    using (new GUILayout.VerticalScope())
                    {
                        EditorGUILayout.PropertyField(propGraph, GUIUtil.GetGridBuilderFieldContent());

                        using (new EditorGUI.DisabledScope(disabledScope))
                        {
                            if (GUILayout.Button("Build Graph"))
                            {
                                buildCallback?.Invoke();
                            }
                        }

                        if (extras == null)
                            return;
                        
                        foreach (var property in extras)
                        {
                            EditorGUILayout.PropertyField(property);
                        }
                    }
                }
            }
        }

        public static void DrawGraphProperties(bool disabledScope, (string text, float value)[] topics, float width)
        {
            if (!disabledScope)
            {
                DrawInfo(topics, width);
            }
        }
        
        public static void DrawGridBox(ViewType viewType, float height)
        {
            GUILayout.Box(GUIUtil.GetGameGridIconTexture(viewType), EditorStyles.helpBox, GUILayout.Width(height), GUILayout.Height(height));
        }

        public static void DrawHexBox(ViewType viewType, float height)
        {
            GUILayout.Box(GUIUtil.GetHexGameGridIconTexture(viewType), EditorStyles.helpBox, GUILayout.Width(height), GUILayout.Height(height));
        }
        
        
        public static void DrawGridBox(ViewType viewType)
        {
            GUILayout.Box(GUIUtil.GetGameGridIconTexture(viewType), EditorStyles.helpBox, GUILayout.Width(50), GUILayout.Height(50));
        }

        public static void DrawHexBox(ViewType viewType)
        {
            GUILayout.Box(GUIUtil.GetHexGameGridIconTexture(viewType), EditorStyles.helpBox, GUILayout.Width(50), GUILayout.Height(50));
        }

        private static void DrawGridIcon(GridType gridType, ViewType viewType)
        {
            switch (gridType)
            {
                case GridType.Square:
                    DrawGridBox(viewType);
                    break;
                case GridType.Hexagonal:
                    DrawHexBox(viewType);
                    break;
            }
        }

        private static void DrawGridIcon(GridType gridType, ViewType viewType, float fixedHeight)
        {
            if (gridType == GridType.Square)
                DrawGridBox(viewType, fixedHeight);
            else
                DrawHexBox(viewType, fixedHeight);
        }

        public static void DrawProperty(bool hideCondition , string buttonText, SerializedProperty property, GUIContent guiContent)
        {
            if(hideCondition)
                return;

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Space(5);
                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(property, guiContent);
                }

                GUILayout.Space(5);
            }
        }

        public static void DrawTwoToggleButtonsH(ref bool toggle, (string b1, string b2) text, (VoidDelegate c1, VoidDelegate c2) callbacks)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(toggle))
                    {
                        if (GUILayout.Button(text.b1))
                        {
                            toggle = true;
                            callbacks.c1?.Invoke();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!toggle))
                    {
                        if (GUILayout.Button(text.b2))
                        {
                            toggle = false;
                            callbacks.c2?.Invoke();
                        }
                    }
                }
            }
        }

        public static void DrawTwoToggleButtonsV(ref bool toggle, (string b1, string b2) text, (VoidDelegate c1, VoidDelegate c2) callbacks)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledScope(!toggle))
                    {
                        if (GUILayout.Button(text.b1))
                        {
                            toggle = false;
                            callbacks.c1();
                        }
                    }

                    using (new EditorGUI.DisabledScope(toggle))
                    {
                        if (GUILayout.Button(text.b2))
                        {
                            toggle = true;
                            callbacks.c2();
                        }
                    }
                }
            }
        }

        public static void DrawTwoToggleButtonsV(ViewType viewType, (string b1, string b2) text, (VoidDelegate c1, VoidDelegate c2) callbacks, int fixedHeight)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(fixedHeight)))
            {
                using (new GUILayout.VerticalScope())
                {
                    using (new EditorGUI.DisabledScope(viewType == ViewType.View3D))
                    {
                        if (GUILayout.Button(text.b1))
                        {
                            callbacks.c1();
                        }
                    }

                    using (new EditorGUI.DisabledScope(viewType == ViewType.View2D))
                    {
                        if (GUILayout.Button(text.b2))
                        {
                            callbacks.c2();
                        }
                    }
                }
            }
        }

        public static void DrawTwoButtons(bool disable1, bool disable2, (string b1, string b2) text, (VoidDelegate c1, VoidDelegate c2) callbacks)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(disable1))
                    {
                        if (GUILayout.Button(text.b1))
                        {
                            callbacks.c1();
                        }
                    }

                    using (new EditorGUI.DisabledScope(disable2))
                    {
                        if (GUILayout.Button(text.b2))
                        {
                            callbacks.c2();
                        }
                    }
                }
            }
        }

        public static void DrawOneButton(bool disable, string text, VoidDelegate callback)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(disable))
                {
                    if (GUILayout.Button(text))
                    {
                        callback();
                    }
                }
            }
        }

        public static void DrawLabelCentered(string text, int padding)
        {
            GUILayout.Space(padding);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(text);
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(padding);
        }

        public static void DrawTexture(Texture2D texture, Rect rect)
        {
            GUI.DrawTexture(rect, texture);
        }
    }
}