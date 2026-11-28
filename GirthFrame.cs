/* Copyright 2024 Naelstrof & Raliv
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
 * documentation files (the “Software”), to deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
 * Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
 * OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GatorDragonGames.DPG {

#if UNITY_EDITOR
using UnityEditor;

[CustomPropertyDrawer(typeof(GirthFrame))]
public class GirthSliceCurveDrawer : PropertyDrawer {
    private const float GraphHeight = 120f;
    private const float LabelWidth = 55f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded) {
            height += EditorGUIUtility.standardVerticalSpacing + GraphHeight;
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);

        Rect header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);

        if (property.isExpanded) {
            Rect graphArea = new Rect(
                position.x,
                header.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                GraphHeight
            );

            if (property.hasMultipleDifferentValues) {
                EditorGUI.HelpBox(graphArea, "Selected curves have different values.", MessageType.Info);
            } else {
                var frame = (GirthFrame)property.boxedValue;
                DrawGraph(graphArea, frame);
            }
        }

        EditorGUI.EndProperty();
    }

    private static void DrawGraph(Rect area, GirthFrame frame) {
        float min = 0f;
        float max = 0f;
        for (int i = 0; i < GirthSliceCurve.sampleCount; i++) {
            float radius = frame.girthCurve.GetSample(i).radius;
            if (float.IsNaN(radius) || float.IsInfinity(radius)) {
                EditorGUI.HelpBox(area, "The curve contains a non-finite radius.", MessageType.Warning);
                return;
            }
            min = Mathf.Min(min, radius);
            max = Mathf.Max(max, radius);
        }

        if (Mathf.Approximately(min, max)) {
            max = min + 1f;
        }

        Rect graph = new Rect(
            area.x + LabelWidth,
            area.y + 2f,
            Mathf.Max(1f, area.width - LabelWidth - 4f),
            area.height - 22f
        );

        EditorGUI.DrawRect(graph, new Color(0.12f, 0.12f, 0.12f));
        GUI.Label(new Rect(area.x, graph.y, LabelWidth, 20f), max.ToString("F4", CultureInfo.CurrentCulture), EditorStyles.miniLabel);
        GUI.Label(new Rect(area.x, graph.yMax - 16f, LabelWidth, 20f), min.ToString("F4", CultureInfo.CurrentCulture), EditorStyles.miniLabel);
        GUI.Label(new Rect(graph.x, graph.yMax + 2f, graph.width, 18f),
            "Sample position: 0 to 1   |   Radius", EditorStyles.miniLabel);

        if (Event.current.type != EventType.Repaint) {
            return;
        }

        var points = new Vector3[GirthSliceCurve.sampleCount];
        for (int i = 0; i < GirthSliceCurve.sampleCount; i++) {
            float x = GirthSliceCurve.sampleCount == 1 ? 0f : (float)i / (GirthSliceCurve.sampleCount - 1);
            float y = Mathf.InverseLerp(min, max, frame.girthCurve.GetSample(i).radius);
            points[i] = new Vector3(
                Mathf.Lerp(graph.x, graph.xMax, x),
                Mathf.Lerp(graph.yMax, graph.y, y),
                0f
            );
        }

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        Handles.color = Color.cyan;
        if (points.Length == 1) {
            EditorGUI.DrawRect(new Rect(points[0].x - 2f, points[0].y - 2f, 4f, 4f), Color.cyan);
        } else {
            Handles.DrawAAPolyLine(2f, points);
        }
        Handles.color = previousColor;
        Handles.EndGUI();
    }
}
#endif

[System.Serializable]
public struct GirthFrame {
    public static Matrix4x4 GetDefaultPenetratorPose() => Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.up,-Vector3.forward), Vector3.one);

    public float maxGirthRadius;
    public float maxLocalLength;
    
    public GirthSliceCurve girthCurve;

    public GirthSlice SampleGirth(float alpha) {
        return girthCurve.Sample(alpha);
    }
    
    public void TransformToWorld(Matrix4x4 transform) {
        var mat = transform*GetDefaultPenetratorPose();
        var radiusScale = mat.MultiplyVector(Vector3.right).magnitude;
        var samples = GirthSliceCurve.sampleCount;
        for (int i = 0; i < samples; i++) {
            var slice = girthCurve.GetSample(i);
            slice.position = mat.MultiplyPoint3x4(slice.position);
            slice.radius *= radiusScale;
            girthCurve.SetSample(i, slice);
        }
    }
    
    public GirthFrame(float fullLength, float maxGirth) {
        maxGirthRadius = maxGirth;
        maxLocalLength = fullLength;
        girthCurve = new GirthSliceCurve();
    }

    public void DrawGizmos(int samples, Color startColor, Color endColor) {
        var lastPosition = Vector3.forward * -0.1f;
        for (int i = 0; i < samples; i++) {
            float alpha = (float)i / (float)(samples - 1);
            Gizmos.color = Color.Lerp(startColor, endColor, alpha);
            GirthSlice slice = SampleGirth(alpha);
            var diff = (Vector3)slice.position - lastPosition;
            DrawCircle(slice.position, diff.normalized, slice.radius, 16);
            lastPosition = slice.position;
        }
    }
    
    private void DrawCircle(Vector3 position, Vector3 normal, float radius, int segments = 16) {
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f) {
            tangent = Vector3.Cross(normal, Vector3.right);
        }
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        bitangent.Normalize();

        Vector3 prevPoint = position + tangent * radius;
        for (int i = 1; i <= segments; i++) {
            float angle = (float)i / (float)segments * Mathf.PI * 2f;
            Vector3 nextPoint = position + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    public void SampleBlendshapes(GirthFrame[] blendshapes, SkinnedMeshGirth.SkinnedMeshGirthSampler sampler) {
        var blendshapeCount = blendshapes.Length;
        using var girthFrame = new NativeReference<GirthFrame>(this, Allocator.TempJob);
        using var blendShapeGirths = new NativeArray<GirthFrame>(blendshapes, Allocator.TempJob);
        using var blendshapeWeights = new NativeArray<float>(sampler.blendshapes, Allocator.TempJob);
        using var output = new NativeArray<GirthSlice>(GirthSliceCurve.sampleCount, Allocator.TempJob);
        var job = new SampleBlendshapesJob(
            girthFrame,
            blendShapeGirths,
            blendshapeWeights,
            output,
            blendshapeCount
        );
        job.Schedule(GirthSliceCurve.sampleCount, 16).Complete();
        girthCurve.CopyFrom(job.output);

        for (int j = 0; j < blendshapeCount; j++) {
            float weight = sampler.blendshapes[j] * 0.01f;
            if (weight == 0f) {
                continue;
            }
            maxGirthRadius += blendshapes[j].maxGirthRadius * weight;
            maxLocalLength += blendshapes[j].maxLocalLength * weight;
        }
    }

    [BurstCompile]
    public struct SampleBlendshapesJob : IJobParallelFor {
        [ReadOnly] public NativeReference<GirthFrame> baseGirth;
        [ReadOnly] public NativeArray<GirthFrame> blendShapeGirths;
        [ReadOnly] public NativeArray<float> blendshapes;
        [WriteOnly] public NativeArray<GirthSlice> output;
        public readonly int blendshapeCount;
        
        public SampleBlendshapesJob(NativeReference<GirthFrame> baseGirth, NativeArray<GirthFrame> blendShapeGirths, NativeArray<float> blendshapes, NativeArray<GirthSlice> output, int blendshapeCount) {
            this.baseGirth = baseGirth;
            this.blendShapeGirths = blendShapeGirths;
            this.blendshapes = blendshapes;
            this.output = output;
            this.blendshapeCount = blendshapeCount;
        }

        public void Execute(int index) {
            var girthSlice = baseGirth.Value.girthCurve.GetSample(index);
            for (int i = 0; i < blendshapeCount; i++) {
                float weight = blendshapes[i] * 0.01f;
                girthSlice += blendShapeGirths[i].girthCurve.GetSample(index) * weight;
            }
            output[index] = girthSlice;
        }
    }

    [BurstCompile]
    public struct SampleTransformsJob : IJobParallelFor {
        public NativeArray<GirthSlice> slices;

        [ReadOnly] public NativeArray<Matrix4x4> boneMatrices;
        [ReadOnly] public NativeArray<Matrix4x4> bindPoses;

        public Matrix4x4 penetratorBindPose;
        public Matrix4x4 worldToRoot;
        public Matrix4x4 inversePenetratorPose;

        public void Execute(int index) {
            var slice = slices[index];
            var meshPos = penetratorBindPose.MultiplyPoint3x4(slice.position);

            var worldPos =
                boneMatrices[slice.boneIndex0].MultiplyPoint3x4(
                    bindPoses[slice.boneIndex0].MultiplyPoint3x4(meshPos)) * slice.boneWeights.x +
                boneMatrices[slice.boneIndex1].MultiplyPoint3x4(
                    bindPoses[slice.boneIndex1].MultiplyPoint3x4(meshPos)) * slice.boneWeights.y +
                boneMatrices[slice.boneIndex2].MultiplyPoint3x4(
                    bindPoses[slice.boneIndex2].MultiplyPoint3x4(meshPos)) * slice.boneWeights.z +
                boneMatrices[slice.boneIndex3].MultiplyPoint3x4(
                    bindPoses[slice.boneIndex3].MultiplyPoint3x4(meshPos)) * slice.boneWeights.w;

            slice.position = inversePenetratorPose.MultiplyPoint3x4(
                worldToRoot.MultiplyPoint3x4(worldPos));

            var meshRadial = penetratorBindPose.MultiplyVector(Vector3.right);
            var worldRadial =
                boneMatrices[slice.boneIndex0].MultiplyVector(
                    bindPoses[slice.boneIndex0].MultiplyVector(meshRadial)) * slice.boneWeights.x +
                boneMatrices[slice.boneIndex1].MultiplyVector(
                    bindPoses[slice.boneIndex1].MultiplyVector(meshRadial)) * slice.boneWeights.y +
                boneMatrices[slice.boneIndex2].MultiplyVector(
                    bindPoses[slice.boneIndex2].MultiplyVector(meshRadial)) * slice.boneWeights.z +
                boneMatrices[slice.boneIndex3].MultiplyVector(
                    bindPoses[slice.boneIndex3].MultiplyVector(meshRadial)) * slice.boneWeights.w;

            slice.radius *= worldToRoot.MultiplyVector(worldRadial).magnitude;
            slices[index] = slice;
        }
    }

    public void SampleTransforms(Matrix4x4[] transformBindPoses, Matrix4x4[] localToWorlds, Matrix4x4 penetratorBindPose, Transform rootBone) {
        int sampleCount = GirthSliceCurve.sampleCount;
        var boneCount = localToWorlds.Length;
        using var slices = new NativeArray<GirthSlice>(sampleCount, Allocator.TempJob);
        using var boneMatrices = new NativeArray<Matrix4x4>(boneCount, Allocator.TempJob);
        using var bindPoses = new NativeArray<Matrix4x4>(boneCount, Allocator.TempJob);
        
        girthCurve.CopyTo(slices);
        boneMatrices.CopyFrom(localToWorlds);
        bindPoses.CopyFrom(transformBindPoses);

        var job = new SampleTransformsJob {
            slices = slices,
            boneMatrices = boneMatrices,
            bindPoses = bindPoses,
            penetratorBindPose = penetratorBindPose,
            worldToRoot = rootBone.worldToLocalMatrix,
            inversePenetratorPose = GetDefaultPenetratorPose().inverse
        };
        job.Schedule(sampleCount, 16).Complete();
        girthCurve.CopyFrom(slices);
    }
    
    
    
    public void ConvertToBlendshape(GirthFrame baseShape) {
        var samples = GirthSliceCurve.sampleCount;
        var blendshapeGirthCurve = girthCurve;
        for (int i = 0; i < samples; i++) {
            var baseGirth = baseShape.girthCurve.GetSample(i);
            var girthDelta = girthCurve.GetSample(i) - baseGirth;
            blendshapeGirthCurve.SetSample(i, girthDelta);
        }
        girthCurve = blendshapeGirthCurve;
        maxLocalLength -= baseShape.maxLocalLength;
        maxGirthRadius -= baseShape.maxGirthRadius;
    }

    private static List<Vector3> staticVertices = new List<Vector3>();
    private static List<int> staticTriangles = new List<int>();
    private static Material staticMaterialA;
    private static Material staticMaterialB;
    private static Material subtractBlit;
    public static async Task<GirthFrame> GenerateGirthFrame(SkinnedMeshRendererSubMeshMask skinnedMeshRendererMask, Transform penetratorRoot, Matrix4x4 penetratorBindPose, int blendshapeIndex, float maxGirth, Shader girthUnwrapShader) {
        GirthFrame girthFrame = new GirthFrame(1, maxGirth);
        var girthMap = new RenderTexture(GirthSliceCurve.sampleCount, GirthSliceCurve.sampleCount, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear) {
            useMipMap = true,
            autoGenerateMips = false,
            wrapModeU = TextureWrapMode.Clamp,
            wrapModeV = TextureWrapMode.Repeat
        };
        var mesh = skinnedMeshRendererMask.renderer.sharedMesh;
        staticVertices.Clear();
        mesh.GetVertices(staticVertices);
        if (blendshapeIndex != -1) {
            Vector3[] blendDeltaVertices = new Vector3[staticVertices.Count];
            mesh.GetBlendShapeFrameVertices(blendshapeIndex, 0, blendDeltaVertices, null, null);
            for (int i = 0; i < staticVertices.Count; i++) {
                staticVertices[i] += blendDeltaVertices[i];
            }
        }

        Mesh blitMesh;
        bool freemesh = false;

        // If we're a skinned mesh renderer, we mask by bone weights.
        var bones = skinnedMeshRendererMask.renderer.bones;
        freemesh = true;
        blitMesh = new Mesh();
        blitMesh.SetVertices(staticVertices);
        blitMesh.subMeshCount = mesh.subMeshCount;
        for (int i = 0; i < mesh.subMeshCount; i++) {
            if (skinnedMeshRendererMask.ShouldDrawSubmesh(i)) {
                staticTriangles.Clear();
                mesh.GetTriangles(staticTriangles, i);
                blitMesh.SetTriangles(staticTriangles, i);
            }
        }

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
                    Vector3 pos = staticVertices[vt];
                    float length = Vector3.Dot(penetratorBindPose.GetColumn(2), pos - (Vector3)penetratorBindPose.GetColumn(3));
                    girthFrame.maxLocalLength = Mathf.Max(length, girthFrame.maxLocalLength);
                }

                wt++;
            }

            vt++;
        }

        if (staticMaterialA == null) {
            staticMaterialA = new Material(girthUnwrapShader);
        }

        if (staticMaterialB == null) {
            staticMaterialB = new Material(girthUnwrapShader);
        }

        staticMaterialA.SetFloat("_AngleOffset", Mathf.PI / 2f);
        staticMaterialA.SetVector("_PenetratorOrigin", (Vector3)penetratorBindPose.GetColumn(3));
        staticMaterialA.SetVector("_PenetratorForward", penetratorBindPose.GetColumn(2).normalized);
        staticMaterialA.SetVector("_PenetratorUp", penetratorBindPose.GetColumn(1).normalized);
        staticMaterialA.SetVector("_PenetratorRight", penetratorBindPose.GetColumn(0).normalized);
        staticMaterialA.SetFloat("_MaxLength", girthFrame.maxLocalLength);
        staticMaterialA.SetFloat("_MaxGirth", girthFrame.maxGirthRadius);
        staticMaterialB.SetFloat("_AngleOffset", -Mathf.PI / 2f);
        staticMaterialB.SetVector("_PenetratorOrigin", (Vector3)penetratorBindPose.GetColumn(3));
        staticMaterialB.SetVector("_PenetratorForward", penetratorBindPose.GetColumn(2).normalized);
        staticMaterialB.SetVector("_PenetratorUp", penetratorBindPose.GetColumn(1).normalized);
        staticMaterialB.SetVector("_PenetratorRight", penetratorBindPose.GetColumn(0).normalized);
        staticMaterialB.SetFloat("_MaxLength", girthFrame.maxLocalLength);
        staticMaterialB.SetFloat("_MaxGirth", girthFrame.maxGirthRadius);

        // Then use the GPU to rasterize
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetRenderTarget(girthMap);
        buffer.ClearRenderTarget(false, true, Color.clear);
        for (int j = 0; j < mesh.subMeshCount; j++) {
            if (!skinnedMeshRendererMask.ShouldDrawSubmesh(j)) {
                continue;
            }

            buffer.DrawMesh(blitMesh, Matrix4x4.identity, staticMaterialA, j, 0);
            buffer.DrawMesh(blitMesh, Matrix4x4.identity, staticMaterialB, j, 0);
        }

        Graphics.ExecuteCommandBuffer(buffer);
        buffer.Dispose();

        girthMap.GenerateMips();

        girthFrame = await Readback(girthFrame, girthMap);
        
        girthFrame = BindToNearestVertices(girthFrame, penetratorBindPose, mesh);

        if (freemesh) {
            if (Application.isPlaying) {
                Object.Destroy(blitMesh);
            }
            else {
                Object.DestroyImmediate(blitMesh);
            }
        }

        return girthFrame;
    }

    private static GirthFrame DoSlowReadback(GirthFrame girthFrame, RenderTexture girthMap) {
        Texture2D cpuTex = new Texture2D(girthMap.width, girthMap.height, TextureFormat.R8, false, true);
        var lastActive = RenderTexture.active;
        RenderTexture.active = girthMap;
        cpuTex.ReadPixels(new Rect(0, 0, girthMap.width, girthMap.height), 0, 0);
        cpuTex.Apply();
        RenderTexture.active = lastActive;
        var bytes = cpuTex.GetRawTextureData<byte>();
        girthFrame = PopulateGirthSlices(girthFrame, bytes, girthMap.width, girthMap.height);
        if (Application.isPlaying) {
            Object.Destroy(cpuTex);
        }
        else {
            Object.DestroyImmediate(cpuTex);
        }
        return girthFrame;
    }

    public static async Task<GirthFrame> Readback(GirthFrame girthFrame, RenderTexture girthMap) {
        if (SystemInfo.supportsAsyncGPUReadback && Application.isPlaying) {
            var nativeArray = new NativeArray<byte>(girthMap.width * girthMap.height * sizeof(byte), Allocator.Persistent);
            var request = AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, girthMap, 0, TextureFormat.R8);
            await Task.Run(() => request.WaitForCompletion());
            if (request.hasError || !request.done) {
                nativeArray.Dispose();
                return DoSlowReadback(girthFrame, girthMap);
            }
            // Otherwise our data is good.
            girthFrame = PopulateGirthSlices(girthFrame, nativeArray, request.width, request.height);
            nativeArray.Dispose();
            return girthFrame;
        } else {
            return DoSlowReadback(girthFrame, girthMap);
        }
    }

    private static GirthFrame BindToNearestVertices(GirthFrame input, Matrix4x4 penetratorBindPose, Mesh mesh) {
        var vertices = mesh.vertices;
        var vertexCount = vertices.Length;
        var boneWeights = mesh.boneWeights;
        var samples = GirthSliceCurve.sampleCount;
        for (int i = 0; i < samples; i++) {
            var girthSlice = input.girthCurve.GetSample(i);
            var localPos = penetratorBindPose.MultiplyPoint(girthSlice.position);
            float nearestDistance = float.MaxValue;
            int nearestVertexIndex = -1;
            for (int v = 0; v < vertexCount; v++) {
                var vertex = vertices[v];
                var distance = Vector3.Distance(localPos, vertex);
                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearestVertexIndex = v;
                }
            }
            if (nearestVertexIndex != -1) {
                var weight = boneWeights[nearestVertexIndex];
                girthSlice.boneWeights = new half4((half)weight.weight0, (half)weight.weight1, (half)weight.weight2, (half)weight.weight3);
                girthSlice.boneIndex0 = (short)weight.boneIndex0;
                girthSlice.boneIndex1 = (short)weight.boneIndex1;
                girthSlice.boneIndex2 = (short)weight.boneIndex2;
                girthSlice.boneIndex3 = (short)weight.boneIndex3;
                input.girthCurve.SetSample(i, girthSlice);
            }
        }

        return input;
    }

    private static GirthFrame PopulateGirthSlices(GirthFrame input, NativeArray<byte> bytes, int width, int height) {
        input.girthCurve = new GirthSliceCurve();
        for (int x = 0; x < width; x++) {
            GirthSlice slice = new GirthSlice();
            Vector2 positionSum = Vector2.zero;
            for (int y = 0; y < height / 2; y++) {
                float color = (float)bytes[x + y * width] / 255f; //cpuTex.GetPixel(x,y).r;
                float rad = ((float)y / (float)height) * Mathf.PI * 2f;
                float distFromCore = color * input.maxGirthRadius;
                float xPosition = Mathf.Sin(rad - Mathf.PI / 2f) * distFromCore;
                float yPosition = Mathf.Cos(rad - Mathf.PI / 2f) * distFromCore;
                Vector2 position = new Vector2(xPosition, yPosition);

                int oppositeY = (y + height / 2) % height;
                float oppositeColor = ((float)bytes[x + oppositeY * width] / 255f); //cpuTex.GetPixel(x,oppositeY).r;
                float oppositeRad = ((float)oppositeY / (float)height) * Mathf.PI * 2f;
                float oppositeDistFromCore = oppositeColor * input.maxGirthRadius;
                float oppositeXPosition = Mathf.Sin(oppositeRad - Mathf.PI / 2f) * oppositeDistFromCore;
                float oppositeYPosition = Mathf.Cos(oppositeRad - Mathf.PI / 2f) * oppositeDistFromCore;
                Vector2 oppositePosition = new Vector2(oppositeXPosition, oppositeYPosition);
                positionSum += (position + oppositePosition) * 0.5f;
            }

            Vector2 positionAverage = positionSum / (float)(height / 2);
            positionAverage *= 2;

            slice.position = new Vector3(positionAverage.x, positionAverage.y, input.maxLocalLength*(float)x/(float)width);

            float averageRadius = 0f;
            for (int y = 0; y < height; y++) {
                float color = (float)bytes[x + y * width] / 255f; //cpuTex.GetPixel(x,y).r;
                float rad = ((float)y / (float)height) * Mathf.PI * 2f;
                float distFromCore = color * input.maxGirthRadius;
                float xPosition = Mathf.Sin(rad - Mathf.PI / 2f) * distFromCore;
                float yPosition = Mathf.Cos(rad - Mathf.PI / 2f) * distFromCore;
                Vector2 position = new Vector2(xPosition, yPosition);
                averageRadius += Vector2.Distance(position, new Vector2(slice.position.x, slice.position.y));
            }
            
            averageRadius /= height;
            // 10% of the dick is tapered at the end, to make it continuous
            int taperEndCount = Mathf.Max(width / 10, 3);
            if (x > width - taperEndCount) {
                float multiplier = (float)(width - x) / (taperEndCount - 1);
                slice.radius = averageRadius * multiplier * multiplier;
            } else {
                slice.radius = averageRadius;
            }
            input.girthCurve.SetSample(x, slice);
        }
        var copy = input.girthCurve.GetSample(width - 2);
        copy.radius = 0f;
        input.girthCurve.SetSample(width - 1, copy);
        return input;
    }

    private static float GetEasing(float x) {
        return x >= 1f ? 1f : 1f - Mathf.Pow(2, -10f * x);
    }

}

}