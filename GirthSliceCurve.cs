using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace GatorDragonGames.DPG {

[Serializable]
public struct GirthSlice {
    // Packed to be exactly 32 bytes
    public float radius;
    public float3 position;
    public half4 boneWeights;
    public short boneIndex0;
    public short boneIndex1;
    public short boneIndex2;
    public short boneIndex3;
    
    public static GirthSlice Lerp(GirthSlice a, GirthSlice b, float t) {
        GirthSlice result = new GirthSlice {
            radius = math.lerp(a.radius, b.radius, t),
            position = math.lerp(a.position, b.position, t),
            boneWeights = (half4)math.lerp(a.boneWeights, b.boneWeights, t),
            boneIndex0 = a.boneIndex0,
            boneIndex1 = a.boneIndex1,
            boneIndex2 = a.boneIndex2,
            boneIndex3 = a.boneIndex3
        };
        return result;
    }
    public static GirthSlice operator +(GirthSlice a, GirthSlice b) {
        GirthSlice result = new GirthSlice {
            radius = a.radius + b.radius,
            position = a.position + b.position,
            boneWeights = a.boneWeights,
            boneIndex0 = a.boneIndex0,
            boneIndex1 = a.boneIndex1,
            boneIndex2 = a.boneIndex2,
            boneIndex3 = a.boneIndex3
        };
        return result;
    }
    public static GirthSlice operator -(GirthSlice a, GirthSlice b) {
        GirthSlice result = new GirthSlice {
            radius = a.radius - b.radius,
            position = a.position - b.position,
            boneWeights = a.boneWeights,
            boneIndex0 = a.boneIndex0,
            boneIndex1 = a.boneIndex1,
            boneIndex2 = a.boneIndex2,
            boneIndex3 = a.boneIndex3
        };
        return result;
    }
    public static GirthSlice operator *(GirthSlice a, float b) {
        GirthSlice result = new GirthSlice {
            radius = a.radius * b,
            position = a.position * b,
            boneWeights = a.boneWeights,
            boneIndex0 = a.boneIndex0,
            boneIndex1 = a.boneIndex1,
            boneIndex2 = a.boneIndex2,
            boneIndex3 = a.boneIndex3
        };
        return result;
    }
}

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct GirthSliceCurve {
    public const int sampleCount = 32;
    
    [SerializeField] private GirthSlice sample0;
    [SerializeField] private GirthSlice sample1;
    [SerializeField] private GirthSlice sample2;
    [SerializeField] private GirthSlice sample3;
    [SerializeField] private GirthSlice sample4;
    [SerializeField] private GirthSlice sample5;
    [SerializeField] private GirthSlice sample6;
    [SerializeField] private GirthSlice sample7;
    [SerializeField] private GirthSlice sample8;
    [SerializeField] private GirthSlice sample9;
    [SerializeField] private GirthSlice sample10;
    [SerializeField] private GirthSlice sample11;
    [SerializeField] private GirthSlice sample12;
    [SerializeField] private GirthSlice sample13;
    [SerializeField] private GirthSlice sample14;
    [SerializeField] private GirthSlice sample15;
    [SerializeField] private GirthSlice sample16;
    [SerializeField] private GirthSlice sample17;
    [SerializeField] private GirthSlice sample18;
    [SerializeField] private GirthSlice sample19;
    [SerializeField] private GirthSlice sample20;
    [SerializeField] private GirthSlice sample21;
    [SerializeField] private GirthSlice sample22;
    [SerializeField] private GirthSlice sample23;
    [SerializeField] private GirthSlice sample24;
    [SerializeField] private GirthSlice sample25;
    [SerializeField] private GirthSlice sample26;
    [SerializeField] private GirthSlice sample27;
    [SerializeField] private GirthSlice sample28;
    [SerializeField] private GirthSlice sample29;
    [SerializeField] private GirthSlice sample30;
    [SerializeField] private GirthSlice sample31;
    
    public void SetSample(int index, GirthSlice value) {
        switch(index) {
            case 0: sample0 = value; break;
            case 1: sample1 = value; break;
            case 2: sample2 = value; break;
            case 3: sample3 = value; break;
            case 4: sample4 = value; break;
            case 5: sample5 = value; break;
            case 6: sample6 = value; break;
            case 7: sample7 = value; break;
            case 8: sample8 = value; break;
            case 9: sample9 = value; break;
            case 10: sample10 = value; break;
            case 11: sample11 = value; break;
            case 12: sample12 = value; break;
            case 13: sample13 = value; break;
            case 14: sample14 = value; break;
            case 15: sample15 = value; break;
            case 16: sample16 = value; break;
            case 17: sample17 = value; break;
            case 18: sample18 = value; break;
            case 19: sample19 = value; break;
            case 20: sample20 = value; break;
            case 21: sample21 = value; break;
            case 22: sample22 = value; break;
            case 23: sample23 = value; break;
            case 24: sample24 = value; break;
            case 25: sample25 = value; break;
            case 26: sample26 = value; break;
            case 27: sample27 = value; break;
            case 28: sample28 = value; break;
            case 29: sample29 = value; break;
            case 30: sample30 = value; break;
            case 31: sample31 = value; break;
            default: throw new IndexOutOfRangeException($"Invalid sample index: {index}");
        }
    }

    public GirthSlice GetSample(int index) {
        switch (index) {
            case 0: return sample0;
            case 1: return sample1;
            case 2: return sample2;
            case 3: return sample3;
            case 4: return sample4;
            case 5: return sample5;
            case 6: return sample6;
            case 7: return sample7;
            case 8: return sample8;
            case 9: return sample9;
            case 10: return sample10;
            case 11: return sample11;
            case 12: return sample12;
            case 13: return sample13;
            case 14: return sample14;
            case 15: return sample15;
            case 16: return sample16;
            case 17: return sample17;
            case 18: return sample18;
            case 19: return sample19;
            case 20: return sample20;
            case 21: return sample21;
            case 22: return sample22;
            case 23: return sample23;
            case 24: return sample24;
            case 25: return sample25;
            case 26: return sample26;
            case 27: return sample27;
            case 28: return sample28;
            case 29: return sample29;
            case 30: return sample30;
            case 31: return sample31;
            default: throw new IndexOutOfRangeException($"Invalid sample index: {index}");
        }
    }
    public unsafe void CopyFrom(NativeArray<GirthSlice> source) {
        if (!source.IsCreated || source.Length != sampleCount) {
            throw new ArgumentException(
                $"Source must be a created array of {sampleCount} slices.",
                nameof(source));
        }

        if (sizeof(GirthSliceCurve) != sampleCount * sizeof(GirthSlice)) {
            throw new InvalidOperationException("GirthSliceCurve layout has changed.");
        }

        fixed (GirthSlice* destination = &sample0) {
            UnsafeUtility.MemCpy(
                destination,
                source.GetUnsafeReadOnlyPtr(),
                (long)sampleCount * sizeof(GirthSlice));
        }
    }

    public unsafe void CopyTo(NativeArray<GirthSlice> destination) {
        if (!destination.IsCreated || destination.Length != sampleCount) {
            throw new ArgumentException(
                $"Destination must be a created array of {sampleCount} slices.",
                nameof(destination));
        }

        if (sizeof(GirthSliceCurve) != sampleCount * sizeof(GirthSlice)) {
            throw new InvalidOperationException("GirthSliceCurve layout has changed.");
        }

        fixed (GirthSlice* source = &sample0) {
            UnsafeUtility.MemCpy(
                destination.GetUnsafePtr(),
                source,
                (long)sampleCount * sizeof(GirthSlice));
        }
    }

    public GirthSlice Sample(float time) {
        var maxIndex = sampleCount - 1;
        var normalizedTime = time;
        int loc = (int)(normalizedTime * (float)maxIndex);
        if (loc >= (maxIndex)) {
            return GetSample(maxIndex);
        }

        if (loc <= 0) {
            return GetSample(0);
        }

        float lerp = (normalizedTime * maxIndex) - loc;
        return GirthSlice.Lerp(GetSample(loc), GetSample(loc + 1), lerp);
    }

    public GirthSlice GetPiecewiseDerivative(float time) {
        float epsilon = 0.001f;
        GirthSlice a = Sample(time - epsilon);
        GirthSlice b = Sample(time + epsilon);
        return (b - a) * (1f/(epsilon * 2f));
    }
}

}