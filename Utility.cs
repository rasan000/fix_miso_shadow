using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;
using NUnit.Framework.Constraints;

namespace Miso.Utility
{
    public static class MisoUtils
    {
        const string CREATE_PATH = "Assets/MISO/Created/";
        const string OBJECT_NAME_STRING = "MisoShadow";
        const string ASSET_PATH = "Assets/MISO/ShadowMenu.asset";
        const string SHADER_SHORT_NAME = "lil";
        const string SHADOW_ANGLE = "Assets/MISO/Animation/Shadow_angle.anim";
        const string SHADOW_BODY = "Assets/MISO/Animation/Shadow_Strength_Body.anim";
        const string SHADOW_ETC = "Assets/MISO/Animation/Shadow_Strength_Etc.anim";
        const string SHADOW_CONTROLLER = "Assets/MISO/Animation/ShadowController.controller";
        const string DUMMY = "Assets/MISO/Animation/Dummy.anim";

        public static Guid CreateGUID()
        {
            Guid NewGUID = Guid.NewGuid();
            return NewGUID;
        }

        public static void AddMAObject(GameObject Avatar, AnimatorController Con)
        {
            Transform CheckObject = Avatar.transform.Find(OBJECT_NAME_STRING);

            if (CheckObject != null)
            {
                GameObject.DestroyImmediate(CheckObject.gameObject);
            }

            GameObject ShadowObject = new GameObject(OBJECT_NAME_STRING);
            ShadowObject.transform.SetParent(Avatar.transform);

            SetupComponents(ShadowObject, Con, ASSET_PATH);
        }

        private static void SetupComponents(GameObject ShadowObject, AnimatorController Con, string assetPath)
        {
            ModularAvatarMergeAnimator MaAnimator = ShadowObject.AddComponent<ModularAvatarMergeAnimator>();
            ModularAvatarMenuInstaller MaMenu = ShadowObject.AddComponent<ModularAvatarMenuInstaller>();
            ModularAvatarParameters MaPara = ShadowObject.AddComponent<ModularAvatarParameters>();

            MaAnimator.animator = Con;
            MaAnimator.deleteAttachedAnimator = true;
            MaAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            MaAnimator.matchAvatarWriteDefaults = true;

            MaMenu.menuToAppend = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(assetPath);

            AddParameters(MaPara);
        }

        private static void AddParameters(ModularAvatarParameters MaPara)
        {
            ParameterConfig[] configs = new ParameterConfig[3];

            configs[0] = new ParameterConfig
            {
                nameOrPrefix = "Shadow_Strength",
                syncType = ParameterSyncType.Bool,
                saved = true
            };

            configs[1] = new ParameterConfig
            {
                nameOrPrefix = "Shadow_Angle",
                syncType = ParameterSyncType.Float,
                saved = true
            };

            configs[2] = new ParameterConfig
            {
                nameOrPrefix = "Toggle_Angle",
                syncType = ParameterSyncType.Bool,
                saved = true
            };

            MaPara.parameters.AddRange(configs);
        }

        public static string GetPartialPath(Transform t) 
        {
            List<string> pathList = new List<string>();
            while (t.parent != null)
            {
                pathList.Insert(0, t.name);
                t = t.parent;
            }

            return string.Join("/", pathList);
        }

        public static List<string> CheckShader(GameObject Avatar)
        {
            Renderer[] Renderers = Avatar.GetComponentsInChildren<Renderer>(true);
            List<string> NameList = new List<string>();

            foreach (Renderer Ren in Renderers)
            {
                Material[] Materials = Ren.sharedMaterials;

                if (Materials == null || Materials.Length == 0)
                {
                    continue;
                }

                foreach (Material Mat in Materials)
                {
                    if (Mat == null)
                    {
                        continue;
                    }

                    if (Mat.shader.name.Contains(SHADER_SHORT_NAME))
                    {
                        string objPath = GetPartialPath(Ren.transform);
                        NameList.Add(objPath);
                    }
                }
            }
            return NameList;
        }

        public static AnimationClip CreateOriginalAnimation(GameObject Avatar, List<string> NameList, Guid GUID)
        {
            if (Avatar == null)
            {
                Debug.LogError("Avatar is null.");
                return null;
            }

            if (NameList == null || NameList.Count == 0)
            {
                Debug.LogError("NameList is null or empty.");
                return null;
            }

            AnimationClip SavedClip = new AnimationClip { name = "SavedShadow_" + Avatar.name };
            AnimationClip OriginalClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SHADOW_BODY);

            if (OriginalClip == null)
            {
                Debug.LogError("Original animation clip not found.");
                return null;
            }

            string NewAnimationPath = CREATE_PATH + Avatar.name + "_" + GUID + "/";
            string SavePath = NewAnimationPath + SavedClip.name + ".anim";

            EditorCurveBinding[] CurveBindings = AnimationUtility.GetCurveBindings(OriginalClip);

            foreach (string name in NameList)
            {
                Transform objTransform = Avatar.transform.Find(name);
                if (objTransform == null)
                {
                    Debug.LogError("Could not find transform: " + name);
                    continue;
                }
                string transformPath = AnimationUtility.CalculateTransformPath(objTransform, Avatar.transform);
                GameObject obj = objTransform.gameObject;
                SkinnedMeshRenderer smr = obj.GetComponent<SkinnedMeshRenderer>();
                MeshRenderer mr = obj.GetComponent<MeshRenderer>();
                Material mat = null;
                Type currentBindingType = null;
                if (smr == null && mr != null)
                {
                    currentBindingType = typeof(MeshRenderer);
                    mat = mr.sharedMaterial;
                }
                else if (mr == null && smr != null)
                {
                    currentBindingType = typeof(SkinnedMeshRenderer);
                    mat = smr.sharedMaterial;
                }
                else
                {
                    Debug.LogError("SkinnedMeshRenderer or MeshRenderer is not exist for GameObject with name " + name + ".");
                }

                if (mat == null)
                {
                    Debug.LogError("Material is null for GameObject with name " + name + ".");
                    continue;
                }

                foreach (EditorCurveBinding binding in CurveBindings)
                {
                    EditorCurveBinding newBinding = binding;
                    if (currentBindingType == typeof(MeshRenderer) && binding.type == typeof(SkinnedMeshRenderer))
                    {
                        newBinding.type = typeof(MeshRenderer);
                    }

                    if (newBinding.type == currentBindingType)
                    {
                        string propertyName = binding.propertyName.Split('.')[1];
                        if (mat.HasProperty(propertyName))
                        {
                            string basePropertyName = binding.propertyName.Split('.')[1];
                            string curveName = "material." + basePropertyName;

                            if (propertyName.ToLower().Contains("color"))
                            {
                                Color colorValue = mat.GetColor(basePropertyName);
                                foreach (char channel in new[] { 'r', 'g', 'b', 'a' })
                                {
                                    float channelValue;
                                    switch (channel)
                                    {
                                        case 'r':
                                            channelValue = colorValue.r;
                                            break;
                                        case 'g':
                                            channelValue = colorValue.g;
                                            break;
                                        case 'b':
                                            channelValue = colorValue.b;
                                            break;
                                        case 'a':
                                            channelValue = colorValue.a;
                                            break;
                                        default:
                                            continue;
                                    }
                                    AnimationCurve curve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, channelValue) });
                                    SavedClip.SetCurve(transformPath, currentBindingType, $"{curveName}.{channel}", curve);
                                }
                            }
                            else
                            {
                                float floatValue = mat.GetFloat(propertyName);
                                AnimationCurve curve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, floatValue) });
                                SavedClip.SetCurve(transformPath, currentBindingType, binding.propertyName, curve);
                            }
                        }
                    }
                }
            }

            if (!Directory.Exists(NewAnimationPath))
            {
                Directory.CreateDirectory(NewAnimationPath);
            }

            AssetDatabase.CreateAsset(SavedClip, SavePath);
            AssetDatabase.SaveAssets();
            return SavedClip;
        }

        public static (AnimationClip, AnimationClip) CreateAndCopyAnimation(GameObject Avatar, List<string> NameList, Guid GUID)
        {
            AnimationClip NewAngleClip = new AnimationClip { name = "ShadowAngle_" + Avatar.name };
            AnimationClip NewStrengthClip = new AnimationClip { name = "Shadow_Strength_" + Avatar.name };
            AnimationClip AngleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SHADOW_ANGLE);
            AnimationClip[] StrengthClips = new AnimationClip[2];
            StrengthClips[0] = AssetDatabase.LoadAssetAtPath<AnimationClip>(SHADOW_BODY);
            StrengthClips[1] = AssetDatabase.LoadAssetAtPath<AnimationClip>(SHADOW_ETC);

            string NewAnimationPath = CREATE_PATH + Avatar.name + "_" + GUID + "/";
            if (!Directory.Exists(NewAnimationPath))
            {
                Directory.CreateDirectory(NewAnimationPath);
            }
            (NewAngleClip, NewStrengthClip) = ProcessingCreateAndCopyAnimation(Avatar, NewAnimationPath, NameList, AngleClip, NewAngleClip, NewStrengthClip, StrengthClips);
            return (NewAngleClip, NewStrengthClip);
        }

        private static (AnimationClip, AnimationClip) ProcessingCreateAndCopyAnimation(GameObject Avatar, string NewAnimationPath, List<string> NameList, AnimationClip AngleClip, AnimationClip NewAngleClip, AnimationClip NewStrengthClip, AnimationClip[] StrengthClips)
        {
            string AnglePath = NewAnimationPath + NewAngleClip.name + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(AnglePath) != null)
            {
                AssetDatabase.DeleteAsset(AnglePath);
            }
            CopyClip(Avatar, NameList, AngleClip, NewAngleClip, false, true);
            AssetDatabase.CreateAsset(NewAngleClip, AnglePath);

            string StrenghPath = NewAnimationPath + NewStrengthClip.name + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(StrenghPath) != null)
            {
                AssetDatabase.DeleteAsset(StrenghPath);
            }
            CopyClip(Avatar, NameList, StrengthClips[0], NewStrengthClip, false, false);
            CopyClip(Avatar, NameList, StrengthClips[1], NewStrengthClip, true, false);
            AssetDatabase.CreateAsset(NewStrengthClip, StrenghPath);
            AssetDatabase.SaveAssets();
            return (NewAngleClip, NewStrengthClip);
        }

        private static void CopyClip(GameObject Avatar, List<string> NameList, AnimationClip SourceClip, AnimationClip DestClip, bool ExcludeFilter = false, bool IgnoreFilter = false)
        {
            foreach (EditorCurveBinding Binding in AnimationUtility.GetCurveBindings(SourceClip))
            {
                AnimationCurve Curve = AnimationUtility.GetEditorCurve(SourceClip, Binding);
                ApplyToAllChildren(Avatar.transform, NameList, Avatar, Binding, Curve, DestClip, ExcludeFilter, IgnoreFilter);
            }
        }

        private static void ApplyToAllChildren(Transform Parent, List<string> NameList, GameObject Avatar, EditorCurveBinding Binding, AnimationCurve Curve, AnimationClip DestClip, bool ExcludeFilter, bool IgnoreFilter)
        {
            Regex bodyRegex = new Regex(".*body.*", RegexOptions.IgnoreCase);

            for (int i = 0; i < Parent.childCount; i++)
            {
                Transform Child = Parent.GetChild(i);
                string fullPath = AnimationUtility.CalculateTransformPath(Child, Avatar.transform);
                bool Apply = NameList.Contains(fullPath);

                if (Apply && !IgnoreFilter)
                {
                    if (ExcludeFilter)
                    {
                        Apply = !bodyRegex.IsMatch(Child.gameObject.name);
                    }
                    else
                    {
                        Apply = bodyRegex.IsMatch(Child.gameObject.name);
                    }
                }

                if (Apply)
                {
                    System.Type typeToUse = null;
                    if (Child.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        typeToUse = typeof(SkinnedMeshRenderer);
                    }
                    else if (Child.GetComponent<MeshRenderer>() != null)
                    {
                        typeToUse = typeof(MeshRenderer);
                    }

                    if (typeToUse != null)
                    {
                        EditorCurveBinding newBinding = new EditorCurveBinding
                        {
                            type = typeToUse,
                            path = AnimationUtility.CalculateTransformPath(Child.transform, Avatar.transform),
                            propertyName = Binding.propertyName
                        };
                        AnimationUtility.SetEditorCurve(DestClip, newBinding, Curve);
                    }
                }

                // Recursive call for child objects
                ApplyToAllChildren(Child, NameList, Avatar, Binding, Curve, DestClip, ExcludeFilter, IgnoreFilter);
            }
        }

        public static AnimatorController ModifyAndSaveController(GameObject Avatar, AnimationClip SavedClip, AnimationClip AngleClip, AnimationClip StrengthClip, Guid GUID)
        {
                        
            AnimatorController OriCon = AssetDatabase.LoadAssetAtPath<AnimatorController>(SHADOW_CONTROLLER);
            
            string NewConPath = CREATE_PATH + Avatar.name + "_" + GUID + "/ShadowController_" + Avatar.name + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(NewConPath) != null)
            {
                AssetDatabase.DeleteAsset(NewConPath);
            }            
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(OriCon), NewConPath);
            AnimatorController NewCon = AssetDatabase.LoadAssetAtPath<AnimatorController>(NewConPath);

            AnimatorControllerLayer StrLayer = NewCon.layers[0];  // Assuming the ShadowStrength layer is at index 1
            foreach (ChildAnimatorState childState in StrLayer.stateMachine.states)
            {
                if (childState.state.name == "Original")
                {
                    childState.state.motion = SavedClip;
                }
                else if (childState.state.name == "Strength")
                {
                    childState.state.motion = StrengthClip;
                }
            }

            AnimatorControllerLayer AngleLayer = NewCon.layers[1];
            foreach (ChildAnimatorState childState in AngleLayer.stateMachine.states)
            {
                if (childState.state.name == "Idle")
                {
                    AnimationClip DummyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DUMMY);
                    childState.state.motion = DummyClip;
                }
                else if (childState.state.name == "Angle")
                {
                    childState.state.motion = AngleClip;
                }
            }

            AssetDatabase.SaveAssets();

            return NewCon;
        }
    
        public static Color ReadShadowColorFromAnimation(string TargetAnim)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TargetAnim);

            if (clip == null)
            {
                Debug.LogError("Animation Clip not found.");
                return Color.clear;
            }

            Color ShadowColor = Color.clear;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Length == 0) continue;

                float value = curve.keys[0].value;
                switch (binding.propertyName)
                {
                    case "material._ShadowColor.r":
                        ShadowColor.r = value;
                        break;
                    case "material._ShadowColor.g":
                        ShadowColor.g = value;
                        break;
                    case "material._ShadowColor.b":
                        ShadowColor.b = value;
                        break;
                    case "material._ShadowColor.a":
                        ShadowColor.a = value;
                        break;
                }
            }
            return ShadowColor;
        }

        public static void SetShadowColorInAnimation(string TargetAnim, Color NewColor)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TargetAnim);
            if (clip == null)
            {
                Debug.LogError("Animation Clip not found.");
                return;
            }

            EditorCurveBinding binding;
            if (TargetAnim == SHADOW_BODY)
            {
                binding = new EditorCurveBinding
                {
                    type = typeof(SkinnedMeshRenderer),
                    path = "Body",
                };
            }
            else
            {
                binding = new EditorCurveBinding
                {
                    type = typeof(SkinnedMeshRenderer),
                    path = "Hair",
                };
            }

            // Red
            AnimationCurve redCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, NewColor.r) });
            binding.propertyName = "material._ShadowColor.r";
            AnimationUtility.SetEditorCurve(clip, binding, redCurve);

            // Green
            AnimationCurve greenCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, NewColor.g) });
            binding.propertyName = "material._ShadowColor.g";
            AnimationUtility.SetEditorCurve(clip, binding, greenCurve);

            // Blue
            AnimationCurve blueCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, NewColor.b) });
            binding.propertyName = "material._ShadowColor.b";
            AnimationUtility.SetEditorCurve(clip, binding, blueCurve);

            // Alpha
            AnimationCurve alphaCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0, NewColor.a) });
            binding.propertyName = "material._ShadowColor.a";
            AnimationUtility.SetEditorCurve(clip, binding, alphaCurve);

            // Save changes
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        public static void SetAnchorOverrideAndBoundsRecursively(Transform Transform, Transform ChestBone)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = Transform.GetComponent<SkinnedMeshRenderer>();
            Vector3 NewCenter = new Vector3(0f, 0f, 0f);
            Vector3 NewExtents = new Vector3(1f, 1f, 1f);

            if (skinnedMeshRenderer != null )
            {
                skinnedMeshRenderer.rootBone = ChestBone;
                skinnedMeshRenderer.probeAnchor = ChestBone;
                Bounds bounds = skinnedMeshRenderer.localBounds;
                bounds.center = NewCenter;
                bounds.extents = NewExtents;
                skinnedMeshRenderer.localBounds = bounds;
            }

            // Continue for all children
            for (int i = 0; i < Transform.childCount; i++)
            {
                SetAnchorOverrideAndBoundsRecursively(Transform.GetChild(i), ChestBone);
            }
        }
    }
}
