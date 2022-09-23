using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimationClipUtil
{
    public class FixBone : AnimationClipUtilBase
    {
        BoneNode fixNode = new BoneNode();
        float referTime;
        bool canFix;

        public FixBone(AnimationClipUtilWindow window) : base(window) { }

        public override void Draw(bool refresh)
        {
            EditorGUILayout.LabelField("Fix Bone:");

            EditorGUI.BeginDisabledGroup(window.selectClip == null);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(23);
            referTime = EditorGUILayout.FloatField("Refer time", referTime);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                if (referTime < 0)
                    referTime = 0;
                else if (referTime > window.selectClip.length)
                    referTime = window.selectClip.length;
                canFix &= referTime >= 0 && referTime <= window.selectClip.length;
            }
            EditorGUI.EndDisabledGroup();

            fixNode.BeginFoldout();
            EditorGUI.BeginChangeCheck();
            Transform fixTrans = EditorGUILayout.ObjectField(fixNode.Self, typeof(Transform), true) as Transform;
            if (EditorGUI.EndChangeCheck() || refresh)
            {
                string path = "";
                canFix = window.anime != null && window.selectClip != null && window.GetPath(window.anime.transform, fixTrans, out path) && referTime >= 0 && referTime <= window.selectClip.length;
                fixNode = new BoneNode(fixTrans, path);
            }
            fixNode.weight = EditorGUILayout.Slider(fixNode.weight, 0, 1, GUILayout.Width(50));
            fixNode.EndFoldout();
            if (fixNode.unfold)
                fixNode.DrawChildren(10);

            if (!canFix)
                EditorGUILayout.HelpBox("Check the config", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!canFix);
            if (GUILayout.Button("Fix Bone"))
                fixBone();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Zero", GUILayout.Width(50)))
                fixNode.ZeroWeight();
            EditorGUILayout.EndHorizontal();
        }

        void fixBone()
        {
            RuntimeAnimatorController origin = window.anime.runtimeAnimatorController;
            AnimatorOverrideController controller = new AnimatorOverrideController(window.anime.runtimeAnimatorController);
            controller[window.ClipName(window.selectClip.name)] = window.selectClip;
            window.anime.runtimeAnimatorController = controller;
            AnimationClip newClip = fixBone(controller, window.selectClip, fixNode);
            window.anime.runtimeAnimatorController = origin;
            EditorUtility.SetDirty(window.anime);

            if (newClip == null)
                return;
            //Writing the new file of clip
            string assetPath = AssetDatabase.GetAssetPath(window.selectClip);
            string[] strs = assetPath.Split('/');
            string path = "";
            for (int i = 0; i < strs.Length - 1; i++)
                path += strs[i] + "/";
            path += $"{newClip.name}.anim";
            window.Save(newClip, path);
        }

        AnimationClip fixBone(AnimatorOverrideController controller, AnimationClip clip, BoneNode node)
        {
            if (window.anime == null || clip == null || node == null || string.IsNullOrEmpty(node.path))
                return null;
            AnimationClip newClip;
            if (node.weight > 0)
            {
                newClip = new AnimationClip();
                newClip.name = window.ClipName(clip.name);
                EditorCurveBinding[] binds = AnimationUtility.GetCurveBindings(clip);
                AnimationClipSettings setting = AnimationUtility.GetAnimationClipSettings(clip);
                AnimationUtility.SetAnimationClipSettings(newClip, setting);

                window.anime.Rebind();
                window.anime.Play(clip.name, -1, referTime / clip.length);
                window.anime.Update(0);

                //Record the bone's fix location and fix rotation
                Vector3 fixPositionWS = node.Self.position;
                Quaternion fixRotationWS = node.Self.rotation;
                List<Vector3> fixPositions = new List<Vector3>();
                List<Quaternion> fixRotations = new List<Quaternion>();
                float rate = 1 / clip.frameRate;
                float len = clip.length * clip.frameRate + 1;

                window.anime.Play(clip.name, -1, 0);
                window.anime.Update(0);
                for (int i = 0; i < len; i++)
                {
                    Vector3 pos = node.Self.parent.InverseTransformPoint(fixPositionWS);
                    Quaternion rot = Quaternion.Inverse(node.Self.parent.rotation) * fixRotationWS;
                    fixPositions.Add(Vector3.LerpUnclamped(node.Self.localPosition, pos, node.weight));
                    rot = Quaternion.LerpUnclamped(node.Self.localRotation, rot, node.weight);
                    fixRotations.Add(rot);
                    if (i + 2 >= len)
                        window.anime.Update(rate * .999f);
                    else
                        window.anime.Update(rate);
                }

                //First inject the origin data
                for (int i = 0; i < binds.Length; i++)
                {
                    EditorCurveBinding bind = binds[i];
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, bind);
                    newClip.SetCurve(bind.path, bind.type, bind.propertyName, curve);
                }

                //Overwrite the bone's fix data
                InjectFixData(node.path, newClip, len, rate, fixPositions, fixRotations);
            }
            else
                newClip = clip;

            //newClip.EnsureQuaternionContinuity();
            for (int i = 0; i < node.Children.Count; i++)
            {
                BoneNode child = node.Children[i];
                controller[clip.name] = newClip;
                newClip = fixBone(controller, newClip, child);
            }
            return newClip;
        }

        void InjectFixData(string path, AnimationClip clip, float len, float rate, List<Vector3> fixPositions, List<Quaternion> fixRotations)
        {
            List<Keyframe> pos_keyframes_x = new List<Keyframe>();
            List<Keyframe> pos_keyframes_y = new List<Keyframe>();
            List<Keyframe> pos_keyframes_z = new List<Keyframe>();
            List<Keyframe> rot_keyframes_x = new List<Keyframe>();
            List<Keyframe> rot_keyframes_y = new List<Keyframe>();
            List<Keyframe> rot_keyframes_z = new List<Keyframe>();
            List<Keyframe> rot_keyframes_w = new List<Keyframe>();
            for (int i = 0; i < len; i++)
            {
                float time = Mathf.Min(rate * i, clip.length);
                pos_keyframes_x.Add(new Keyframe(time, fixPositions[i].x));
                pos_keyframes_y.Add(new Keyframe(time, fixPositions[i].y));
                pos_keyframes_z.Add(new Keyframe(time, fixPositions[i].z));

                rot_keyframes_x.Add(new Keyframe(time, fixRotations[i].x));
                rot_keyframes_y.Add(new Keyframe(time, fixRotations[i].y));
                rot_keyframes_z.Add(new Keyframe(time, fixRotations[i].z));
                rot_keyframes_w.Add(new Keyframe(time, fixRotations[i].w));
            }
            AnimationCurve position_x = new AnimationCurve(pos_keyframes_x.ToArray());
            AnimationCurve position_y = new AnimationCurve(pos_keyframes_y.ToArray());
            AnimationCurve position_z = new AnimationCurve(pos_keyframes_z.ToArray());
            AnimationCurve rotation_x = new AnimationCurve(rot_keyframes_x.ToArray());
            AnimationCurve rotation_y = new AnimationCurve(rot_keyframes_y.ToArray());
            AnimationCurve rotation_z = new AnimationCurve(rot_keyframes_z.ToArray());
            AnimationCurve rotation_w = new AnimationCurve(rot_keyframes_w.ToArray());
            for (int i = 0; i < len; i++)
            {
                position_x.SmoothTangents(i, 0);
                position_y.SmoothTangents(i, 0);
                position_z.SmoothTangents(i, 0);
                rotation_x.SmoothTangents(i, 0);
                rotation_y.SmoothTangents(i, 0);
                rotation_z.SmoothTangents(i, 0);
                rotation_w.SmoothTangents(i, 0);
            }
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", position_x);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", position_y);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", position_z);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", rotation_x);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", rotation_y);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", rotation_z);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", rotation_w);
        }
    }
}