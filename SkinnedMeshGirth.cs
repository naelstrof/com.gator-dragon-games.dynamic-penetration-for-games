using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace GatorDragonGames.DPG {


#if UNITY_EDITOR
using UnityEditor;

[CustomPropertyDrawer(typeof(SkinnedMeshGirth))]
public class SkinnedMeshGirthDrawer : PropertyDrawer {
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        if (property.isExpanded) {
            return EditorGUI.GetPropertyHeight(property, label, true)
                   + EditorGUIUtility.standardVerticalSpacing
                   + EditorGUIUtility.singleLineHeight;
        }
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        var stableLabel = new GUIContent(label);
        EditorGUI.BeginProperty(position, stableLabel, property);
        
        float fieldHeight = EditorGUI.GetPropertyHeight(property, stableLabel, true);
        Rect fieldRect = new Rect(position.x, position.y, position.width, fieldHeight);
        EditorGUI.PropertyField(fieldRect, property, stableLabel, true);

        Rect buttonRect = new Rect(
            position.x,
            fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
            position.width,
            EditorGUIUtility.singleLineHeight
        );

        if (property.isExpanded && GUI.Button(EditorGUI.IndentedRect(buttonRect), "Generate Girth Lookup")) { 
            property.serializedObject.ApplyModifiedProperties();
            var skinnedMeshGirth = (SkinnedMeshGirth)property.boxedValue;
            Debug.Log("Attempting to generate girth lookup");
            var task = SkinnedMeshGirth.GenerateGirthLookup(skinnedMeshGirth);
            task.ContinueWith(t => {
                if (t.IsFaulted) {
                    Debug.LogException(t.Exception, property.serializedObject.targetObject);
                } else {
                    property.boxedValue = t.Result;
                    property.serializedObject.ApplyModifiedProperties();
                    Debug.Log("Girth lookup generation completed!");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        EditorGUI.EndProperty();
    }
}

#endif

[Serializable]
public struct SkinnedMeshGirth {
    [SerializeField] public SkinnedMeshRendererSubMeshMask skinnedMeshRendererMask;
    [SerializeField] public Transform[] penetratorBones;
    [SerializeField] public float maxGirth;
    [SerializeField] public GirthFrame baseGirth;
    [SerializeField] public GirthFrame[] girthDeltas;
    [SerializeField] public Matrix4x4 penetratorBindPose;
    
#if UNITY_EDITOR
    public static async Task<SkinnedMeshGirth> GenerateGirthLookup(SkinnedMeshGirth skinnedMeshGirth) {
        Matrix4x4 bindPose = GirthFrame.GetDefaultPenetratorPose();
        var girthUnwrapShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath("34d24fe4568f98c4cae4724e139cb644"));
        return await GenerateGirthLookup(skinnedMeshGirth.skinnedMeshRendererMask, girthUnwrapShader, skinnedMeshGirth.penetratorBones, bindPose);
    }
#endif

    public struct SkinnedMeshGirthSampler : INativeDisposable {
        public Matrix4x4[] localToWorlds;
        public Matrix4x4[] bindPoses;
        private TransformAccessArray bones;
        public float[] blendshapes;

        public SkinnedMeshGirthSampler(SkinnedMeshRenderer renderer) {
            var boneCount = renderer.bones.Length;
            localToWorlds = new Matrix4x4[boneCount];
            bones = new TransformAccessArray(renderer.bones);
            for(int i=0;i<boneCount;i++) {
                localToWorlds[i] = bones[i].localToWorldMatrix;
            }
            bindPoses = renderer.sharedMesh.bindposes;
            blendshapes = new float[renderer.sharedMesh.blendShapeCount];
        }
        
        public void Sample(SkinnedMeshRenderer renderer) {
            var data = new NativeArray<Matrix4x4>(localToWorlds, Allocator.TempJob);
            var job = new SampleBones(data);
            var handle = job.Schedule(bones);
            handle.Complete();
            data.CopyTo(localToWorlds);
            data.Dispose();
            
            var blendshapeLength = blendshapes.Length;
            for(int i=0;i<blendshapeLength;i++) {
                blendshapes[i] = renderer.GetBlendShapeWeight(i);
            }
        }
        [BurstCompile]
        private struct SampleBones : IJobParallelForTransform {
            [WriteOnly] private NativeArray<Matrix4x4> _localToWorlds;
        
            public SampleBones(NativeArray<Matrix4x4> _localToWorlds) {
                this._localToWorlds = _localToWorlds;
            }
        
            public void Execute(int index, TransformAccess bone) {
                _localToWorlds[index] = bone.localToWorldMatrix;
            }
        }

        public void Dispose() {
            bones.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps) {
            bones.Dispose();
            return inputDeps;
        }
    }
    
    public SkinnedMeshGirthSampler GetSampler() {
        return new SkinnedMeshGirthSampler(skinnedMeshRendererMask.renderer);
    }

    public void DrawGizmos(ref GirthFrame girthFrameMemory, SkinnedMeshGirthSampler sampler, int sampleCount = 128) {
        Gizmos.matrix = Matrix4x4.identity;
        GetGirthFrame(ref girthFrameMemory, sampler);
        girthFrameMemory.DrawGizmos(sampleCount, Color.green, Color.red);
    }

    public void GetGirthFrame(ref GirthFrame output, SkinnedMeshGirthSampler sampler) {
        output = baseGirth;
        sampler.Sample(skinnedMeshRendererMask.renderer);
        output.SampleBlendshapes(girthDeltas, sampler);
        output.SampleTransforms(sampler.bindPoses, sampler.localToWorlds, penetratorBindPose, penetratorBones[0]);
        output.TransformToWorld(penetratorBones[0].localToWorldMatrix);
    }

    public static void GetBindPoseBoneLocalPositionRotation(Matrix4x4 boneMatrix, out Vector3 position, out Quaternion rotation, out float scale) {
        // Get global matrix for bone
        Matrix4x4 bindMatrixGlobal = boneMatrix.inverse;

        // Get local X, Y, Z, and position of matrix
        Vector3 mX = new Vector3(bindMatrixGlobal.m00, bindMatrixGlobal.m10, bindMatrixGlobal.m20);
        Vector3 mY = new Vector3(bindMatrixGlobal.m01, bindMatrixGlobal.m11, bindMatrixGlobal.m21);
        Vector3 mZ = new Vector3(bindMatrixGlobal.m02, bindMatrixGlobal.m12, bindMatrixGlobal.m22);
        Vector3 mP = new Vector3(bindMatrixGlobal.m03, bindMatrixGlobal.m13, bindMatrixGlobal.m23);
        position = mP;

        scale = mX.magnitude;

        // Set rotation
        // Check if scaling is negative and handle accordingly
        if (Vector3.Dot(Vector3.Cross(mX, mY), mZ) >= 0) {
            rotation = Quaternion.LookRotation(mZ, mY);
        } else {
            rotation = Quaternion.LookRotation(-mZ, -mY);
        }
    }

    private static float GetMaxGirth(SkinnedMeshRendererSubMeshMask skinnedMeshRendererMask, Transform penetratorRoot, Vector3 rendererLocalPenetratorForward, Vector3 rendererLocalPenetratorRoot, List<Vector3> staticVertices) {
        float maxGirth = 0f;
        staticVertices.Clear();
        var mesh = skinnedMeshRendererMask.renderer.sharedMesh;
        mesh.GetVertices(staticVertices);
        Vector3[] blendDeltaVertices = new Vector3[staticVertices.Count];
        for (int blendshapeIndex = -1; blendshapeIndex < mesh.blendShapeCount; blendshapeIndex++) {
            if (blendshapeIndex != -1) {
                mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, blendDeltaVertices, null, null);
            }
            for (int i = 0; i < staticVertices.Count; i++) {
                blendDeltaVertices[i] += staticVertices[i];
            }

            var bones = skinnedMeshRendererMask.renderer.bones;
            HashSet<int> validBones = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++) {
                if (bones[i].IsChildOf(penetratorRoot)) {
                    validBones.Add(i);
                }
            }

            var weights = mesh.GetAllBoneWeights();
            var bonesPerVertex = mesh.GetBonesPerVertex();
            int vt = 0;
            int wt = 0;
            for (int o = 0; o < bonesPerVertex.Length; o++) {
                for (int p = 0; p < bonesPerVertex[o]; p++) {
                    BoneWeight1 weight = weights[wt];
                    if (validBones.Contains(weight.boneIndex) && weight.weight > 0.1f) {
                        Vector3 pos = blendDeltaVertices[vt];
                        float length = Vector3.Dot(rendererLocalPenetratorForward, pos - rendererLocalPenetratorRoot);
                        float girth = Vector3.Distance(pos, (rendererLocalPenetratorRoot + rendererLocalPenetratorForward * length));
                        maxGirth = Mathf.Max(girth, maxGirth);
                    }
                    wt++;
                }
                vt++;
            }
        }
        return maxGirth;
    }
    public static bool TryGetBoneIndex(Transform[] bones, Transform bone, out int index) {
        for (int i = 0; i < bones.Length; i++) {
            if (bones[i] == bone) {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }
    
    public static async Task<SkinnedMeshGirth> GenerateGirthLookup(SkinnedMeshRendererSubMeshMask skinnedMeshRendererWithMask, Shader girthUnwrapShader, Transform[] bones, Matrix4x4 penetratorLocalPose) {
        var penetratorRoot = bones[0];
        var skinnedMeshRenderer = skinnedMeshRendererWithMask.renderer;
        if (!TryGetBoneIndex(skinnedMeshRenderer.bones, penetratorRoot, out int rootBoneID)) {
            throw new UnityException("You must choose a bone on the armature...");
        }

        Mesh skinnedMesh = skinnedMeshRenderer.sharedMesh;
        GetBindPoseBoneLocalPositionRotation(skinnedMesh.bindposes[rootBoneID], out Vector3 posePosition, out Quaternion poseRotation, out float scale);

        var rendererBindPose = Matrix4x4.TRS(posePosition, poseRotation, new Vector3(scale, scale, scale)) * penetratorLocalPose;

        var staticVertices = new List<Vector3>();
        var maxGirth = GetMaxGirth(skinnedMeshRendererWithMask, penetratorRoot, rendererBindPose.MultiplyVector(Vector3.forward), rendererBindPose.MultiplyPoint(Vector3.zero), staticVertices);
        
        var baseGirth = await GirthFrame.GenerateGirthFrame(skinnedMeshRendererWithMask, penetratorRoot, rendererBindPose, -1, maxGirth, girthUnwrapShader);
        var blendshapeCount = skinnedMeshRendererWithMask.renderer.sharedMesh.blendShapeCount;
        var blendshapeGirths = new GirthFrame[blendshapeCount];
        for (int i = 0; i < blendshapeCount; i++) {
            var blendshapeGirth = await GirthFrame.GenerateGirthFrame(skinnedMeshRendererWithMask, penetratorRoot, rendererBindPose, i, maxGirth, girthUnwrapShader);
            blendshapeGirth.ConvertToBlendshape(baseGirth);
            blendshapeGirths[i] = blendshapeGirth;
        }
        return new SkinnedMeshGirth() {
            baseGirth = baseGirth,
            girthDeltas = blendshapeGirths,
            maxGirth = maxGirth,
            penetratorBindPose = rendererBindPose,
            penetratorBones = bones,
            skinnedMeshRendererMask = skinnedMeshRendererWithMask
        };
    }
}
}