using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using Miso.Utility;
using System;

public class MisoShadow
{
    const string MENU_PATH = "GameObject/Miso Shadow/Add Shadow";
    const string COLOR_PICKER_PATH = "GameObject/Miso Shadow/Change Shadow Colors";

    [MenuItem(MENU_PATH, false, 10)]
    static void AddShadowToSelectedObject(MenuCommand menuCommand)
    {
        GameObject Avatar = menuCommand.context as GameObject;
        VRCAvatarDescriptor Des = Avatar.GetComponent<VRCAvatarDescriptor>();

        if (Des == null)
        {
            EditorUtility.DisplayDialog("Warning", "There is no VRCAvatarDescriptor Component", "Use Correct Avatar Object");
            return;
        }
        List<string> NameList = MisoUtils.CheckShader(Avatar);
        Guid NewGuid = MisoUtils.CreateGUID();
        AnimationClip SavedClip = MisoUtils.CreateOriginalAnimation(Avatar, NameList, NewGuid);
        (AnimationClip AngleClip, AnimationClip StrengthClip) = MisoUtils.CreateAndCopyAnimation(Avatar, NameList, NewGuid);
        AnimatorController Con = MisoUtils.ModifyAndSaveController(Avatar, SavedClip, AngleClip, StrengthClip, NewGuid);
        MisoUtils.AddMAObject(Avatar, Con);

        //確認が面倒なので場合は下記のSetAnchorOverrideのみコメント解除して、34~39行目をコメントアウト
        // SetAnchorOverride(Avatar);
        bool result = EditorUtility.DisplayDialog("Question", "Do you want to reassign all of your Material's Root Bones and Anchor Overrides to Chest Bone?", "Yes", "No");
        if (result)
        {
            SetAnchorOverride(Avatar);
        }
        EditorUtility.DisplayDialog("Success", "Miso Shadow Apply Complete", "OK");


    }

    [MenuItem(COLOR_PICKER_PATH, false, 11)]
    static void ChangeShadowColors(MenuCommand menuCommand)
    {
        ColorPickerWindow.ShowWindow();
    }

    private static void SetAnchorOverride(GameObject TargetAvatar)
    {
        Animator Animator = TargetAvatar.GetComponent<Animator>();
        Transform ChestBone = Animator.GetBoneTransform(HumanBodyBones.Chest);

        if (ChestBone == null)
        {
            EditorUtility.DisplayDialog("Warning", "Chest bone not found.", "OK");
            return;
        }

        MisoUtils.SetAnchorOverrideAndBoundsRecursively(TargetAvatar.transform, ChestBone);
    }
}

public class ColorPickerWindow : EditorWindow
{
    private Color BodyColor;
    private Color EtcColor;
    private const string SHADOW_BODY = "Assets/MISO/Animation/Shadow_Strength_Body.anim";
    private const string SHADOW_ETC = "Assets/MISO/Animation/Shadow_Strength_Etc.anim";
    private Color DefaultBodyShadow = new Color(0.823f, 0.705f, 0.705f, 1f);
    private Color DefaultEtcShadow = new Color(0.590f, 0.590f, 0.590f, 1f);
    private bool isInitialized = false;  // 추가된 필드

    public static void ShowWindow()
    {
        GetWindow<ColorPickerWindow>("Color Picker");
    }

    private void OnGUI()
    {
        if (!isInitialized)
        {
            BodyColor = MisoUtils.ReadShadowColorFromAnimation(SHADOW_BODY);
            EtcColor = MisoUtils.ReadShadowColorFromAnimation(SHADOW_ETC);
            isInitialized = true;
        }

        GUILayout.Label("Select Shadow Color", EditorStyles.boldLabel);

        BodyColor = EditorGUILayout.ColorField("Set Body Shadow Color", BodyColor);
        EtcColor = EditorGUILayout.ColorField("Set Etc Shadow Color", EtcColor);

        if (GUILayout.Button("Apply"))
        {
            MisoUtils.SetShadowColorInAnimation(SHADOW_BODY, BodyColor);
            MisoUtils.SetShadowColorInAnimation(SHADOW_ETC, EtcColor);
            EditorUtility.DisplayDialog("Success", "Miso Shadow Color Chnage Complete", "OK");
        }

        if (GUILayout.Button("Reset"))
        {
            BodyColor = DefaultBodyShadow;
            EtcColor = DefaultEtcShadow;
            MisoUtils.SetShadowColorInAnimation(SHADOW_BODY, DefaultBodyShadow);
            MisoUtils.SetShadowColorInAnimation(SHADOW_ETC, DefaultEtcShadow);
        }
    }
}