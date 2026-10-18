using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Mixamo clips downloaded without a character have no bind pose, so Unity builds each clip's
    /// avatar from its first frame. For the walk that's mid-stride, left knee bent 60°, and the
    /// retargeted walk then bends the knees that much less (stiff, twisted left leg). This gives
    /// every Mixamo clip a real T-pose reference: the bone rotations of a full Mixamo character
    /// (Remy.fbx, same "mixamorig:" bones) copied into the clip's avatar skeleton. Positions keep
    /// the clip's own proportions.
    /// </summary>
    public static class MixamoTPose
    {
        const string ReferencePath = "Assets/Animations/Story/Remy.fbx";
        const string Prefix = "mixamorig:";

        static Dictionary<string, Quaternion> reference;

        static Dictionary<string, Quaternion> Reference
        {
            get
            {
                if (reference != null) return reference;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePath);
                if (model == null) return null;
                reference = new Dictionary<string, Quaternion>();
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith(Prefix)) reference[t.name] = t.localRotation;
                return reference;
            }
        }

        /// <summary>
        /// Rewrites the avatar skeleton of a humanoid Mixamo clip to the T-pose (call after the
        /// importer is set to Human / CreateFromThisModel). Returns false if it isn't a Mixamo rig.
        /// Reimports when it changes anything.
        /// </summary>
        public static bool Apply(ModelImporter importer)
        {
            var refRot = Reference;
            if (importer == null || refRot == null || importer.animationType != ModelImporterAnimationType.Human) return false;
            var hd = importer.humanDescription;
            if (hd.skeleton == null || hd.skeleton.Length == 0)
            {
                // The skeleton list is generated on the first humanoid import.
                importer.SaveAndReimport();
                hd = importer.humanDescription;
                if (hd.skeleton == null || hd.skeleton.Length == 0) return false;
            }
            var skeleton = hd.skeleton;
            int changed = 0;
            for (int i = 0; i < skeleton.Length; i++)
            {
                if (!skeleton[i].name.StartsWith(Prefix) || !refRot.TryGetValue(skeleton[i].name, out var rot)) continue;
                if (Quaternion.Angle(skeleton[i].rotation, rot) < 0.01f) continue;
                skeleton[i].rotation = rot;
                changed++;
            }
            if (changed == 0) return true;
            hd.skeleton = skeleton;
            importer.humanDescription = hd;
            importer.SaveAndReimport();
            return true;
        }

        [MenuItem("Beach Volleyball/Fix Mixamo Clip T-Poses")]
        public static void ApplyAll()
        {
            int n = 0;
            foreach (var dir in new[] { "Assets/Animations/Story", "Assets/Animations/Player" })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.fbx"))
                {
                    var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as ModelImporter;
                    if (Apply(importer)) n++;
                }
            }
            Debug.Log($"[MixamoTPose] T-pose reference applied to {n} clips.");
        }
    }
}
