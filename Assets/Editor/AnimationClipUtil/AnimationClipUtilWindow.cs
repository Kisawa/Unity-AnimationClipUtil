using System;
using UnityEditor;
using UnityEngine;

namespace AnimationClipUtil
{
    public class AnimationClipUtilWindow : EditorWindow
    {
        [MenuItem("Tools/Animation Clip Util Window")]
        static void Open()
        {
            GetWindow<AnimationClipUtilWindow>("Animation Clip Util");
        }

        public Animator anime { get; private set; }
        public AnimationClip selectClip { get; private set; }

        Vector2 scrollPosition;
        AnimationClip[] clips = new AnimationClip[0];

        FixBone util_FixBone;

        private void OnEnable()
        {
            util_FixBone = new FixBone(this);
        }

        private void OnGUI()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

            bool refresh = false;
            EditorGUI.BeginChangeCheck();
            anime = EditorGUILayout.ObjectField(anime, typeof(Animator), true) as Animator;
            if (EditorGUI.EndChangeCheck())
            {
                refresh = true;
                RefreshAnime();
            }
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip _clip = clips[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.Toggle(_clip == selectClip, GUILayout.Width(25));
                if (EditorGUI.EndChangeCheck())
                {
                    if (_clip == selectClip)
                        selectClip = null;
                    else
                        selectClip = _clip;
                    refresh = true;
                }
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(_clip, typeof(AnimationClip), false);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Refresh"))
            {
                refresh = true;
                RefreshAnime();
            }

            GUILayout.Space(10);
            util_FixBone.Draw(refresh);

            GUILayout.EndScrollView();
        }

        void RefreshAnime()
        {
            if (anime == null)
                clips = new AnimationClip[0];
            else
                clips = AnimationUtility.GetAnimationClips(anime.gameObject);
            if (Array.IndexOf(clips, selectClip) == -1)
                selectClip = null;
        }

        public bool GetPath(Transform root, Transform child, out string path)
        {
            path = "";
            if (root == null || child == null)
                return false;
            Transform _trans = child;
            string _path = "";
            while (_trans != root)
            {
                _path = _path.Insert(0, _trans.name + "/");
                _trans = _trans.transform.parent;
                if (_trans == null)
                    return false;
            }
            if (_path.Length > 0)
                path = _path.Remove(_path.Length - 1);
            else
                path = _path;
            return true;
        }

        public string ClipName(string name)
        {
            string[] strs = name.Split(' ');
            if (strs.Length > 0)
            {
                if (int.TryParse(strs[strs.Length - 1], out _))
                {
                    name = "";
                    for (int i = 0; i < strs.Length - 1; i++)
                        name += strs[i] + " ";
                    name = name.TrimEnd();
                    return name;
                }
            }
            return name;
        }

        public void Save(AnimationClip newClip, string path)
        {
            if (newClip == null)
                return;
            string newPath = path;
            int tryIndex = 0;
            while (true)
            {
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(newPath) == null)
                    break;
                else
                {
                    int end = path.LastIndexOf(".");
                    newPath = path.Insert(end, $" {tryIndex}");
                    tryIndex++;
                }
            }
            AssetDatabase.CreateAsset(newClip, newPath);
            AssetDatabase.ImportAsset(newPath);
        }
    }
}