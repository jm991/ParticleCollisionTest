﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParticleCollisionHelper))]
public class ParticleCollisionHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ParticleCollisionHelper phc = target as ParticleCollisionHelper;

        // Show default inspector property editor
        DrawDefaultInspector();

        if (GUILayout.Button("Play") && EditorApplication.isPlaying)
        {
            phc.Play();
        }

        if (GUILayout.Button("Pause") && EditorApplication.isPlaying)
        {
            phc.Pause();
        }
    }
}
