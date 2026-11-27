using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace GatorDragonGames.DPG {

[System.Serializable]
public struct BoneChainOnCurve : INativeDisposable {
    private readonly bool isValid;
    private Transform root;
    private TransformAccessArray _boneChain;
    
    private NativeArray<float3> _sampledBonePositions;

    public struct BoneData {
        public float3 localPosition;
        public quaternion localRotation;
        public float3 localScale;
        public float3 position;
        public quaternion rotation;
    }
    private NativeArray<BoneData> _bindTransforms;
    private NativeArray<BoneData> _restTransforms;
    private NativeArray<BoneData> _lastTransforms;
    private NativeArray<AlignInstructions> _alignInstructions;

    private float _totalLength;

    public BoneChainOnCurve(Transform[] bones) {
        var boneCount = bones.Length;
        root = bones[0];
        _boneChain = new TransformAccessArray(bones);
        _bindTransforms = new NativeArray<BoneData>(boneCount, Allocator.Persistent);
        _restTransforms = new NativeArray<BoneData>(boneCount, Allocator.Persistent);
        _lastTransforms = new NativeArray<BoneData>(boneCount, Allocator.Persistent);
        _sampledBonePositions = new NativeArray<float3>(boneCount, Allocator.Persistent);
        _alignInstructions = new NativeArray<AlignInstructions>(boneCount, Allocator.Persistent);
        
        isValid = true;
        _totalLength = 0f;
        var lastPosition = bones[0].position;
        for (int i=0;i<boneCount;i++) {
            var bone = bones[i];
            var pos = bone.position;
            _totalLength += Vector3.Distance(lastPosition, pos);
            lastPosition = pos;
        }
        var samplePositions = new SamplePose(_bindTransforms);
        _sampleHandle = samplePositions.Schedule(_boneChain);
        _sampleHandle.Complete();
    }
    
    [BurstCompile]
    public struct ResetToRest : IJobParallelForTransform {
        private NativeArray<BoneData> _restTransforms;
        [ReadOnly] private NativeArray<BoneData> _lastTransforms;
        
        public ResetToRest(NativeArray<BoneData> restTransforms, NativeArray<BoneData> lastTransforms) {
            _restTransforms = restTransforms;
            _lastTransforms = lastTransforms;
        }

        public void Execute(int index, TransformAccess bone) {
            bone.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
            var localScale = bone.localScale;
            var lastTransform = _lastTransforms[index];
            var restTransform = _restTransforms[index];
            
            var changed = localScale != (Vector3)lastTransform.localScale || localRotation != lastTransform.localRotation || localPosition != (Vector3)lastTransform.localPosition;
            if (changed) {
                restTransform.localScale = localScale;
                restTransform.localRotation = localRotation;
                restTransform.localPosition = localPosition;
                bone.GetPositionAndRotation(out var pos, out var rot);
                restTransform.position = pos;
                restTransform.rotation = rot;
                _restTransforms[index] = restTransform;
            } else {
                bone.SetLocalPositionAndRotation(restTransform.localPosition, restTransform.localRotation);
                bone.localScale = restTransform.localScale;
            }
        }
    }
    
    [BurstCompile]
    private struct SamplePose : IJobParallelForTransform {
        [WriteOnly] private NativeArray<BoneData> _sampledBonePositions;
        
        public SamplePose(NativeArray<BoneData> sampledBoneDistances) {
            _sampledBonePositions = sampledBoneDistances;
        }
        
        public void Execute(int index, TransformAccess bone) {
            bone.GetLocalPositionAndRotation(out var lpos, out var lrot);
            bone.GetPositionAndRotation(out var pos, out var rot);
            _sampledBonePositions[index] = new BoneData {
                localPosition = lpos,
                localRotation = lrot,
                localScale = bone.localScale,
                position = pos,
                rotation = rot
            };
        }
    }

    [BurstCompile]
    private struct ApplyFinalPose : IJobParallelForTransform {
        [ReadOnly] private NativeArray<AlignInstructions> _alignmentInstructions;
        private float blend;
        
        public ApplyFinalPose(NativeArray<AlignInstructions> alignmentInstructions, float blend) {
            _alignmentInstructions = alignmentInstructions;
            this.blend = blend;
        }
        
        public void Execute(int index, TransformAccess bone) {
            var alignment = _alignmentInstructions[index];
            bone.GetPositionAndRotation(out var pos, out var rot);
            var blendedPos = math.lerp(pos, alignment.worldPosition, blend);
            var blendedRot = math.slerp(rot, alignment.worldRotation, blend);
            bone.SetPositionAndRotation(blendedPos, blendedRot);
        }
    }

    private struct AlignInstructions {
        public float3 worldPosition;
        public quaternion worldRotation;
    }

    [BurstCompile]
    private struct AlignToSpline : IJob {
        [WriteOnly] private NativeArray<AlignInstructions> _output;
        [WriteOnly] private NativeReference<float> _totalLength;
        [ReadOnly] private NativeArray<BoneData> _sampledBoneData;
        private int boneCount;
        private Spline3D targetSpline3D;
        
         public AlignToSpline(NativeArray<AlignInstructions> output, NativeArray<BoneData> sampledBoneData, NativeReference<float> totalLength, int boneCount, Spline3D targetSpline3D) {
            _output = output;
            _sampledBoneData = sampledBoneData;
            this.boneCount = boneCount;
            this.targetSpline3D = targetSpline3D;
            _totalLength = totalLength;
         }
        
        public void Execute() {
            var generalAxis = math.normalize(_sampledBoneData[^1].position - _sampledBoneData[0].position);
            var rootPosition = _sampledBoneData[0].position;
            
            for (var index = 0; index < boneCount; index++) {
                var bonePosition = _sampledBoneData[index].position;
                var boneProjection = math.dot(generalAxis, bonePosition - rootPosition);
                var nextBonePosition = bonePosition + generalAxis * 0.1f;
                var nextBoneProjection = math.dot(generalAxis, nextBonePosition - rootPosition);
                var axis = generalAxis;
                if (index < boneCount - 1) {
                    nextBonePosition = _sampledBoneData[index + 1].position;
                    nextBoneProjection = math.dot(generalAxis, nextBonePosition - rootPosition);
                    axis = math.normalize(_sampledBoneData[index + 1].position - _sampledBoneData[index].position);
                }
                var position = targetSpline3D.GetPositionFromDistance(boneProjection);
                var nextPositionForward = targetSpline3D.GetPositionFromDistance(nextBoneProjection);
                
                var rotation = Quaternion.FromToRotation(axis, math.normalizesafe(nextPositionForward-position, generalAxis));
                _output[index] = new AlignInstructions {
                    worldPosition = position,
                    worldRotation = math.mul(rotation, _sampledBoneData[index].rotation)
                };
            }
            _totalLength.Value = math.dot(generalAxis, _sampledBoneData[^1].position - rootPosition);
        }
    }
    
    private JobHandle _sampleHandle;

    public void StartRender() {
        var resetToRestJob = new ResetToRest(_restTransforms, _lastTransforms);
        var samplePositions = new SamplePose(_lastTransforms);
        var resetToRestHandle = resetToRestJob.Schedule(_boneChain);
        _sampleHandle = samplePositions.Schedule(_boneChain, resetToRestHandle);
    }
    
    public void FinishRender(ref Spline3D onSpline, float blend) {
        if (!isValid) {
            Debug.LogError("BoneChainOnCurve is not valid. Please initialize it with a valid array of bones.");
            return;
        }
        
        using var totalLength = new NativeReference<float>(Allocator.TempJob);
        var alignToSpline = new AlignToSpline(_alignInstructions, _lastTransforms, totalLength, _boneChain.length, onSpline);
        var applyFinalPose = new ApplyFinalPose(_alignInstructions, blend);
        var samplePose = new SamplePose(_lastTransforms);
        
        var alignHandle = alignToSpline.Schedule(_sampleHandle);
        var applyPoseHandle = applyFinalPose.Schedule(_boneChain, alignHandle);
        var sampleFinalHandle = samplePose.Schedule(_boneChain, applyPoseHandle);
        sampleFinalHandle.Complete();
        _totalLength = totalLength.Value;
    }

    public float GetTotalLength() => _totalLength;
    public void Dispose() {
        if (!isValid) {
            return;
        }
        _boneChain.Dispose();
        _sampledBonePositions.Dispose();
        _restTransforms.Dispose();
        _lastTransforms.Dispose();
        _alignInstructions.Dispose();
    }

    public JobHandle Dispose(JobHandle inputDeps) {
        Dispose();
        return inputDeps;
    }
}

}
