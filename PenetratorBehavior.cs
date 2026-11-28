using System.Collections.Generic;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
#if UNITY_EDITOR
#endif

namespace GatorDragonGames.DPG {

public class PenetratorBehavior : MonoBehaviour {
    [SerializeField] private SkinnedMeshGirth skinnedMeshGirth;
    [SerializeField] private PenetrableBehavior penetrableBehavior;
    
    private BoneChainOnCurve boneChainOnCurve;
    private BoneChainCompression boneChainCompression;
    
    private List<Vector3> cachedPoints = new List<Vector3>();
    private Transform[] rendererTransforms;

    private Penetration penetration;
    private GirthFrame penetrationGirthFrame;
    private SkinnedMeshGirth.SkinnedMeshGirthSampler skinnedMeshGirthSampler;
    
    private Transform[] bones => skinnedMeshGirth.penetratorBones;
    private SkinnedMeshRendererSubMeshMask skinnedMeshRendererMask => skinnedMeshGirth.skinnedMeshRendererMask;

    public Transform GetBone(int index) {
        return bones[index];
    }
    
    public int GetBoneCount() {
        return bones.Length;
    }
    
    private Penetrator GetPenetrator() {
        return new Penetrator() {
            position = bones[0].position,
            direction = (bones[1].position - bones[0].position).normalized,
            penetratorLength = boneChainOnCurve.GetTotalLength()
        };
    }

    public Penetration GetPenetration() => penetration;

    private void OnEnable() {
        boneChainOnCurve = new BoneChainOnCurve(bones);
        boneChainCompression = new BoneChainCompression(boneChainOnCurve.GetTotalLength());
        skinnedMeshGirthSampler = skinnedMeshGirth.GetSampler();
        penetrationGirthFrame = new GirthFrame(1f, 0.1f);
        JigglePhysics.JigglePhysics.JigglePoseCompleted += OnJigglePoseCompleted;
    }

    private void OnDisable() {
        JigglePhysics.JigglePhysics.JigglePoseCompleted -= OnJigglePoseCompleted;
        boneChainOnCurve.Dispose();
    }
    
    private void LateUpdate() {
        if (penetrableBehavior == null) {
            return;
        }
        Penetrator penetrator = GetPenetrator();
        var penetrable = penetrableBehavior.GetPenetrable();
        penetration = new Penetration(penetrator, penetrable, cachedPoints);
            
        boneChainOnCurve.StartRender();
    }

    private void OnJigglePoseCompleted() {
        if (penetrableBehavior == null) {
            return;
        }
        
        float jiggleBlendDistance = 1f;
        float penetrableDistance = penetration.spline3D.GetDistanceFromTime(penetration.penetrableT);
        float penetratorTipDistance = penetration.spline3D.GetDistanceFromTime(penetration.tipT);
        float jiggleBlend = Mathf.Clamp01((penetrableDistance - penetratorTipDistance)/jiggleBlendDistance);
        
        var orificeDist = penetration.spline3D.GetDistanceFromTime(penetration.penetrableT);
        var currentLength = boneChainOnCurve.GetTotalLength();
        boneChainCompression.Tick(orificeDist);
        var lengthMultiplier = boneChainCompression.GetTipPosition() / boneChainOnCurve.GetBindLength();
        
        boneChainOnCurve.FinishRender(ref penetration.spline3D, 1f-jiggleBlend, lengthMultiplier);
        
        //var aimPoint = penetration.spline3D.GetPositionFromDistance(boneChainOnCurve.GetTotalLength());
        //Debug.DrawLine(aimPoint - Vector3.up * 0.1f, aimPoint + Vector3.up * 0.1f, Color.red);
        //Debug.DrawLine(aimPoint - Vector3.left * 0.1f, aimPoint + Vector3.left * 0.1f, Color.red);
        //Debug.DrawLine(aimPoint - Vector3.forward * 0.1f, aimPoint + Vector3.forward * 0.1f, Color.red);
        
        skinnedMeshGirth.GetGirthFrame(ref penetrationGirthFrame, skinnedMeshGirthSampler);
        
        PenetratorCollisionSpheresAuto.Render(penetrableBehavior.GetRenderers(), penetrationGirthFrame,0.25f, 0.5f, 32);
    }

    private void OnDrawGizmosSelected() {
        var bones = skinnedMeshGirth.penetratorBones;
        if (Application.isPlaying) {
            if (bones == null || bones.Length < 1 || bones[0] == null) return;
            penetrationGirthFrame.DrawGizmos(GirthSliceCurve.sampleCount, Color.grey, Color.white);
        } else {
            if (skinnedMeshRendererMask.renderer == null) return;
            if (bones == null || bones.Length < 1 || bones[0] == null) return;
            skinnedMeshGirth.DrawGizmos(ref penetrationGirthFrame, skinnedMeshGirth.GetSampler(), GirthSliceCurve.sampleCount);
        }
    }

    public void SetLinkedPenetrable(PenetrableBehavior penetrableBehavior) {
        this.penetrableBehavior = penetrableBehavior;
    }
}

}