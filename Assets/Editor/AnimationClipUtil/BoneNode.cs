using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimationClipUtil
{
    class BoneNode
    {
        public Transform Self { get; private set; }
        public List<BoneNode> Children;
        public string path { get; private set; }
        public bool unfold { get; private set; } = true;
        public float weight = 1;

        public BoneNode() { }

        public BoneNode(Transform trans, string path)
        {
            if (trans == null)
                return;
            Self = trans;
            Children = new List<BoneNode>();
            this.path = path;
            for (int i = 0; i < trans.childCount; i++)
            {
                Transform _trans = trans.GetChild(i);
                BoneNode node = new BoneNode(_trans, $"{path}/{_trans.name}");
                Children.Add(node);
            }
        }

        public void BeginFoldout()
        {
            GUIContent foldout_off = EditorGUIUtility.IconContent("d_IN_foldout_act");
            GUIContent foldout_on = EditorGUIUtility.IconContent("d_IN_foldout_act_on");
            EditorGUILayout.BeginHorizontal();
            if (Children != null && Children.Count > 0)
            {
                if (GUILayout.Button(unfold ? foldout_on : foldout_off, "ObjectPickerTab", GUILayout.Width(20)))
                    unfold = !unfold;
            }
            else
                GUILayout.Space(23);
        }

        public void EndFoldout()
        {
            EditorGUILayout.EndHorizontal();
        }

        public void Draw(float space, int jump = 1)
        {
            BeginFoldout();
            GUILayout.Space(space * jump);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(Self, typeof(Transform), true);
            EditorGUI.EndDisabledGroup();
            weight = EditorGUILayout.Slider(weight, 0, 1, GUILayout.Width(50));
            EndFoldout();
        }

        public void DrawChildren(float space, int jump = 1)
        {
            if (Self == null)
                return;
            for (int i = 0; i < Children.Count; i++)
            {
                BoneNode node = Children[i];
                node.Draw(space, jump);
                if (node.unfold)
                    node.DrawChildren(space, jump + 1);
            }
        }

        public void ZeroWeight()
        {
            if (Self == null)
                return;
            weight = 0;
            for (int i = 0; i < Children.Count; i++)
                Children[i].ZeroWeight();
        }
    }
}