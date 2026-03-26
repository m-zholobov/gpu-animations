using System.Collections.Generic;
using System.IO;
using GPUAnimation.Runtime;
using UnityEditor;
using UnityEngine;

namespace GPUAnimation.Editor
{
    public class AnimationBakerWindow : EditorWindow
    {
        private GameObject _targetObject;
        private int _sampleFPS = 30;
        private string _savePath = "Assets/GPUAnimation/BakedData";
        private string _assetName = "Character";
        private Vector2 _scrollPos;
        private List<AnimationClip> _clips = new();
        private List<bool> _clipEnabled = new();

        [MenuItem("Tools/GPU Animation Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationBakerWindow>("GPU Animation Baker");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("GPU Animation Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            _targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", _targetObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _targetObject != null)
                CollectClips();

            if (_targetObject == null)
            {
                EditorGUILayout.HelpBox("Select Object with SkinnedMeshRenderer and Animator.", MessageType.Info);
                return;
            }

            var smr = _targetObject.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = _targetObject.GetComponentInChildren<Animator>();

            if (smr == null)
            {
                EditorGUILayout.HelpBox("SkinnedMeshRenderer was not found!", MessageType.Error);
                return;
            }

            if (animator == null)
                EditorGUILayout.HelpBox("Animator was not found!", MessageType.Warning);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Bones: {smr.bones.Length}, Vertices: {smr.sharedMesh.vertexCount}");
            EditorGUILayout.Space(5);

            _sampleFPS = EditorGUILayout.IntSlider("Sample FPS", _sampleFPS, 10, 60);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _savePath = EditorGUILayout.TextField("Save Path", _savePath);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh Clips"))
                CollectClips();

            if (_clips.Count == 0)
            {
                EditorGUILayout.HelpBox("There are no animation clips. Add them manually or configure the Animator.", MessageType.Warning);
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(250));
            for (var i = 0; i < _clips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _clipEnabled[i] = EditorGUILayout.Toggle(_clipEnabled[i], GUILayout.Width(20));
                _clips[i] = (AnimationClip)EditorGUILayout.ObjectField(_clips[i], typeof(AnimationClip), false);

                var frames = _clips[i] != null ? Mathf.CeilToInt(_clips[i].length * _sampleFPS) + 1 : 0;
                EditorGUILayout.LabelField($"{frames} fr", GUILayout.Width(50));

                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    _clips.RemoveAt(i);
                    _clipEnabled.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Clip"))
            {
                _clips.Add(null);
                _clipEnabled.Add(true);
            }

            var totalFrames = 0;
            var enabledClipCount = 0;
            for (var i = 0; i < _clips.Count; i++)
            {
                if (_clipEnabled[i] && _clips[i] != null)
                {
                    totalFrames += Mathf.CeilToInt(_clips[i].length * _sampleFPS) + 1;
                    enabledClipCount++;
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Enabled clips: {enabledClipCount}, Total frames: {totalFrames}");
            var texWidth = smr.bones.Length * 3;
            EditorGUILayout.LabelField($"Texture size: {texWidth} x {totalFrames}");
            var memKB = (long)texWidth * totalFrames * 8 / 1024;
            EditorGUILayout.LabelField($"Estimated memory: {memKB} KB");

            EditorGUILayout.Space(10);

            GUI.enabled = enabledClipCount > 0 && totalFrames > 0;
            if (GUILayout.Button("BAKE", GUILayout.Height(40)))
                Bake(smr);
            GUI.enabled = true;
        }

        private void CollectClips()
        {
            _clips.Clear();
            _clipEnabled.Clear();

            if (_targetObject == null)
                return;

            var animator = _targetObject.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (!_clips.Contains(clip))
                    {
                        _clips.Add(clip);
                        _clipEnabled.Add(true);
                    }
                }
            }
        }

        private void Bake(SkinnedMeshRenderer smr)
        {
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
                AssetDatabase.Refresh();
            }

            var bones = smr.bones;
            var bindposes = smr.sharedMesh.bindposes;
            var boneCount = bones.Length;

            var enabledClips = new List<AnimationClip>();
            for (var i = 0; i < _clips.Count; i++)
            {
                if (_clipEnabled[i] && _clips[i] != null)
                    enabledClips.Add(_clips[i]);
            }

            var clipInfos = new GPUAnimClipInfo[enabledClips.Count];
            var totalFrames = 0;
            for (var i = 0; i < enabledClips.Count; i++)
            {
                var clip = enabledClips[i];
                var frameCount = Mathf.CeilToInt(clip.length * _sampleFPS) + 1;
                clipInfos[i] = new GPUAnimClipInfo
                {
                    clipName = clip.name,
                    startFrame = totalFrames,
                    frameCount = frameCount,
                    frameRate = _sampleFPS,
                    loop = clip.isLooping
                };
                totalFrames += frameCount;
            }

            var texWidth = boneCount * 3;
            var texHeight = totalFrames;

            var texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBAHalf, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{_assetName}_AnimTex"
            };

            const int BONE_STRIDE = 3;
            var rowPixels = new Color[texWidth];
            Matrix4x4 worldToLocal = smr.transform.worldToLocalMatrix;

            var animator = _targetObject.GetComponentInChildren<Animator>();
            Transform animRoot = animator != null ? animator.transform : _targetObject.transform;

            var posValues = new Dictionary<Transform, Vector3>(boneCount);
            var rotValues = new Dictionary<Transform, Vector4>(boneCount);
            var scaleValues = new Dictionary<Transform, Vector3>(boneCount);

            var currentFrame = 0;
            try
            {
                for (var clipIdx = 0; clipIdx < enabledClips.Count; clipIdx++)
                {
                    var clip = enabledClips[clipIdx];
                    var frameCount = clipInfos[clipIdx].frameCount;

                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    var curveData = new List<CurveEntry>(bindings.Length);
                    var unresolvedCount = 0;

                    foreach (var binding in bindings)
                    {
                        if (binding.type != typeof(Transform))
                            continue;

                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve == null) continue;

                        var target = string.IsNullOrEmpty(binding.path) ? animRoot : animRoot.Find(binding.path);
                        if (target == null)
                        {
                            unresolvedCount++;
                            continue;
                        }

                        curveData.Add(new CurveEntry
                        {
                            target = target,
                            propertyName = binding.propertyName,
                            curve = curve
                        });
                    }

                    if (unresolvedCount > 0 && clipIdx == 0)
                        Debug.LogWarning($"[Baker] Clip '{clip.name}': {unresolvedCount} unresolved transform paths.");

                    var originalTransforms = new Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale)>();
                    foreach (var entry in curveData)
                    {
                        if (!originalTransforms.ContainsKey(entry.target))
                        {
                            originalTransforms[entry.target] = (entry.target.localPosition, entry.target.localRotation,
                                entry.target.localScale);
                        }
                    }

                    for (var frame = 0; frame < frameCount; frame++)
                    {
                        var time = Mathf.Min((float)frame / _sampleFPS, clip.length);

                        posValues.Clear();
                        rotValues.Clear();
                        scaleValues.Clear();

                        foreach (var entry in curveData)
                        {
                            float val = entry.curve.Evaluate(time);
                            string prop = entry.propertyName;
                            Transform t = entry.target;

                            if (prop.StartsWith("m_LocalPosition"))
                            {
                                if (!posValues.TryGetValue(t, out var p))
                                    p = t.localPosition;
                                if (prop.EndsWith(".x")) p.x = val;
                                else if (prop.EndsWith(".y")) p.y = val;
                                else if (prop.EndsWith(".z")) p.z = val;
                                posValues[t] = p;
                            }
                            else if (prop.StartsWith("m_LocalRotation"))
                            {
                                if (!rotValues.TryGetValue(t, out var r))
                                {
                                    var q = t.localRotation;
                                    r = new Vector4(q.x, q.y, q.z, q.w);
                                }
                                if (prop.EndsWith(".x")) r.x = val;
                                else if (prop.EndsWith(".y")) r.y = val;
                                else if (prop.EndsWith(".z")) r.z = val;
                                else if (prop.EndsWith(".w")) r.w = val;
                                rotValues[t] = r;
                            }
                            else if (prop.StartsWith("m_LocalScale"))
                            {
                                if (!scaleValues.TryGetValue(t, out var s))
                                    s = t.localScale;
                                if (prop.EndsWith(".x")) s.x = val;
                                else if (prop.EndsWith(".y")) s.y = val;
                                else if (prop.EndsWith(".z")) s.z = val;
                                scaleValues[t] = s;
                            }
                        }

                        foreach (var kvp in posValues)
                            kvp.Key.localPosition = kvp.Value;
                        
                        foreach (var kvp in rotValues)
                        {
                            var r = kvp.Value;
                            kvp.Key.localRotation = new Quaternion(r.x, r.y, r.z, r.w);
                        }
                        
                        foreach (var kvp in scaleValues)
                            kvp.Key.localScale = kvp.Value;

                        worldToLocal = smr.transform.worldToLocalMatrix;

                        for (var boneIdx = 0; boneIdx < boneCount; boneIdx++)
                        {
                            Matrix4x4 boneMatrix;
                            if (bones[boneIdx] != null)
                                boneMatrix = worldToLocal * bones[boneIdx].localToWorldMatrix * bindposes[boneIdx];
                            else
                                boneMatrix = Matrix4x4.identity;

                            int px = boneIdx * BONE_STRIDE;
                            rowPixels[px + 0] = new Color(boneMatrix.m00, boneMatrix.m01, boneMatrix.m02, boneMatrix.m03);
                            rowPixels[px + 1] = new Color(boneMatrix.m10, boneMatrix.m11, boneMatrix.m12, boneMatrix.m13);
                            rowPixels[px + 2] = new Color(boneMatrix.m20, boneMatrix.m21, boneMatrix.m22, boneMatrix.m23);
                        }

                        texture.SetPixels(0, currentFrame, texWidth, 1, rowPixels);
                        currentFrame++;

                        if (currentFrame % 10 == 0)
                        {
                            EditorUtility.DisplayProgressBar("Baking Animations",
                                $"Clip: {clip.name} ({frame + 1}/{frameCount})",
                                (float)currentFrame / totalFrames);
                        }
                    }

                    foreach (var kvp in originalTransforms)
                    {
                        kvp.Key.localPosition = kvp.Value.pos;
                        kvp.Key.localRotation = kvp.Value.rot;
                        kvp.Key.localScale = kvp.Value.scale;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            texture.Apply();

            var bakedMesh = CreateBakedMesh(smr);

            var texPath = $"{_savePath}/{_assetName}_AnimTex.asset";
            var meshPath = $"{_savePath}/{_assetName}_Mesh.asset";
            var dataPath = $"{_savePath}/{_assetName}_Data.asset";

            SaveOrReplaceAsset(texture, texPath);
            SaveOrReplaceAsset(bakedMesh, meshPath);

            var data = CreateInstance<GPUAnimationData>();
            data.clips = clipInfos;

            SaveOrReplaceAsset(data, dataPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var createdData = AssetDatabase.LoadAssetAtPath<GPUAnimationData>(dataPath);
            Selection.activeObject = createdData;

            Debug.Log($"[GPU Animation Baker] Baked {enabledClips.Count} clips, {totalFrames} frames, " +
                      $"texture {texWidth}x{texHeight}, {boneCount} bones -> {dataPath}");
        }

        private Mesh CreateBakedMesh(SkinnedMeshRenderer smr)
        {
            var srcMesh = smr.sharedMesh;
            var mesh = Instantiate(srcMesh);
            mesh.name = $"{_assetName}_BakedMesh";

            var boneWeights = srcMesh.boneWeights;
            var vertexCount = srcMesh.vertexCount;
            var uv2 = new Vector4[vertexCount];

            for (var i = 0; i < vertexCount; i++)
            {
                var bw = boneWeights[i];
                var w0 = bw.weight0;
                var w1 = bw.weight1;

                var totalWeight = w0 + w1;
                if (totalWeight > 0f)
                {
                    w0 /= totalWeight;
                    w1 /= totalWeight;
                }
                else
                {
                    w0 = 1f;
                    w1 = 0f;
                }

                uv2[i] = new Vector4(bw.boneIndex0, bw.boneIndex1, w0, w1);
            }

            mesh.SetUVs(2, uv2);
            mesh.boneWeights = null;
            mesh.bindposes = null;

            return mesh;
        }

        private void SaveOrReplaceAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(asset, path);
            }
        }

        private struct CurveEntry
        {
            public Transform target;
            public string propertyName;
            public AnimationCurve curve;
        }
    }
}
