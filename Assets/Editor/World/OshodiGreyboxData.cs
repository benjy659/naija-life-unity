// Data classes for Assets/Data/Oshodi/oshodi-greybox-build.json (made by
// tools/oshodi/oshodi-build-list.js from docs/specs/oshodi-greybox-layout.json).
// Field names match the JSON exactly so JsonUtility can read it. Do not rename.

using System;
using UnityEditor;
using UnityEngine;

namespace NaijaLife.World.Editor
{
    [Serializable]
    public class GreyboxMaterial
    {
        public string key;
        public float r, g, b;
        public float emission;
    }

    [Serializable]
    public class GreyboxItem
    {
        public string id;
        public string scene;     // "core" or "approach"
        public string group;     // hierarchy path under the generated root, "A/B/C"
        public string shape;     // box | cyl | sphere | blocker
        public float x, y, z;    // centre (metres)
        public float sx, sy, sz; // full size (metres)
        public float rx, ry, rz; // Euler degrees
        public string mat;
        public string layer;
        public string collider;  // none | box | primitive | meshConvex
        public bool isStatic;
    }

    [Serializable]
    public class GreyboxMarker
    {
        public string id;
        public string scene;
        public string group;
        public float x, y, z, ry;
    }

    [Serializable]
    public class GreyboxLine
    {
        public string id;
        public float x0, z0, x1, z1, y;
    }

    [Serializable]
    public class GreyboxChecks
    {
        public float overheadMin;
        public float stepMax;
        public GreyboxLine[] laneLines;
        public GreyboxLine[] clearLines;
        public GreyboxLine footbridgeWalk;
    }

    [Serializable]
    public class GreyboxBuildFile
    {
        public int version;
        public string generatedBy;
        public GreyboxMaterial[] materials;
        public GreyboxItem[] items;
        public GreyboxMarker[] markers;
        public GreyboxChecks checks;

        public const string AssetPath = "Assets/Data/Oshodi/oshodi-greybox-build.json";

        public static GreyboxBuildFile Load()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (text == null)
                throw new InvalidOperationException(
                    "Missing " + AssetPath + ". Run: node tools/oshodi/oshodi-layout.js");
            var data = JsonUtility.FromJson<GreyboxBuildFile>(text.text);
            if (data == null || data.items == null || data.items.Length == 0)
                throw new InvalidOperationException("Could not read " + AssetPath);
            return data;
        }
    }
}
