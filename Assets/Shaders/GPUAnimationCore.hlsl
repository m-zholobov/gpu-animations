#ifndef GPU_ANIMATION_CORE_INCLUDED
#define GPU_ANIMATION_CORE_INCLUDED

TEXTURE2D(_AnimationTex);

#define GPU_ANIM_BONE_STRIDE 3

void SampleBoneRows(uint boneIndex, uint frame, out float4 r0, out float4 r1, out float4 r2)
{
    int x = boneIndex * GPU_ANIM_BONE_STRIDE;
    r0 = LOAD_TEXTURE2D(_AnimationTex, int2(x,     frame));
    r1 = LOAD_TEXTURE2D(_AnimationTex, int2(x + 1, frame));
    r2 = LOAD_TEXTURE2D(_AnimationTex, int2(x + 2, frame));
}

void LoadBlendedBoneRows(uint boneIndex, uint frame, uint frameNext, float frameLerp,
                          out float4 r0, out float4 r1, out float4 r2)
{
    SampleBoneRows(boneIndex, frame, r0, r1, r2);
    UNITY_BRANCH
    if (frame != frameNext)
    {
        float4 b0, b1, b2;
        SampleBoneRows(boneIndex, frameNext, b0, b1, b2);
        r0 = lerp(r0, b0, frameLerp);
        r1 = lerp(r1, b1, frameLerp);
        r2 = lerp(r2, b2, frameLerp);
    }
}

float4x4 SampleBoneMatrix(uint boneIndex, uint frame)
{
    float4 r0, r1, r2;
    SampleBoneRows(boneIndex, frame, r0, r1, r2);
    return float4x4(r0, r1, r2, float4(0, 0, 0, 1));
}

float3 SkinPos(float4 r0, float4 r1, float4 r2, float3 p)
{
    float4 ph = float4(p, 1.0);
    return float3(dot(r0, ph), dot(r1, ph), dot(r2, ph));
}

float3 SkinDir(float4 r0, float4 r1, float4 r2, float3 d)
{
    return float3(dot(r0.xyz, d), dot(r1.xyz, d), dot(r2.xyz, d));
}

void SkinClip(float3 posOS, float3 nrmOS, float4 boneData,
              uint frame, uint frameNext, float frameLerp,
              out float3 outPos, out float3 outNrm)
{
    uint bi0 = (uint)boneData.x;
    uint bi1 = (uint)boneData.y;
    float w0 = boneData.z;
    float w1 = boneData.w;

    float4 r0_0, r1_0, r2_0, r0_1, r1_1, r2_1;
    LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, r0_0, r1_0, r2_0);
    LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, r0_1, r1_1, r2_1);

    outPos = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
    outNrm = normalize(SkinDir(r0_0, r1_0, r2_0, nrmOS) * w0 + SkinDir(r0_1, r1_1, r2_1, nrmOS) * w1);
}

void SkinClipFull(float3 posOS, float3 nrmOS, float3 tanDir, float4 boneData,
                  uint frame, uint frameNext, float frameLerp,
                  out float3 outPos, out float3 outNrm, out float3 outTan)
{
    uint bi0 = (uint)boneData.x;
    uint bi1 = (uint)boneData.y;
    float w0 = boneData.z;
    float w1 = boneData.w;

    float4 r0_0, r1_0, r2_0, r0_1, r1_1, r2_1;
    LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, r0_0, r1_0, r2_0);
    LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, r0_1, r1_1, r2_1);

    outPos = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
    outNrm = normalize(SkinDir(r0_0, r1_0, r2_0, nrmOS) * w0 + SkinDir(r0_1, r1_1, r2_1, nrmOS) * w1);
    outTan = normalize(SkinDir(r0_0, r1_0, r2_0, tanDir) * w0 + SkinDir(r0_1, r1_1, r2_1, tanDir) * w1);
}

void ApplyGPUAnimation(inout float3 posOS, inout float3 nrmOS, float4 boneData,
                        uint frame, uint frameNext, float frameLerp)
{
    SkinClip(posOS, nrmOS, boneData, frame, frameNext, frameLerp, posOS, nrmOS);
}

void ApplyGPUAnimationFull(inout float3 posOS, inout float3 nrmOS, inout float4 tanOS, float4 boneData,
                            uint frame, uint frameNext, float frameLerp)
{
    float3 outPos, outNrm, outTan;
    SkinClipFull(posOS, nrmOS, tanOS.xyz, boneData, frame, frameNext, frameLerp, outPos, outNrm, outTan);
    posOS = outPos;
    nrmOS = outNrm;
    tanOS.xyz = outTan;
}

void ApplyGPUAnimationBlend(inout float3 posOS, inout float3 nrmOS, float4 boneData,
                             uint frame, uint frameNext, float frameLerp,
                             uint prevFrame, uint prevFrameNext, float prevFrameLerp,
                             float blendWeight)
{
    uint bi0 = (uint)boneData.x;
    uint bi1 = (uint)boneData.y;
    float w0 = boneData.z;
    float w1 = boneData.w;

    UNITY_BRANCH
    if (blendWeight >= 1.0)
    {
        float4 r0_0, r1_0, r2_0, r0_1, r1_1, r2_1;
        LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, r0_0, r1_0, r2_0);
        LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, r0_1, r1_1, r2_1);

        posOS = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
        nrmOS = normalize(SkinDir(r0_0, r1_0, r2_0, nrmOS) * w0 + SkinDir(r0_1, r1_1, r2_1, nrmOS) * w1);
    }
    else
    {
        float4 pa0, pa1, pa2, ca0, ca1, ca2;
        float4 pb0, pb1, pb2, cb0, cb1, cb2;

        LoadBlendedBoneRows(bi0, prevFrame, prevFrameNext, prevFrameLerp, pa0, pa1, pa2);
        LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, ca0, ca1, ca2);
        float4 r0_0 = lerp(pa0, ca0, blendWeight);
        float4 r1_0 = lerp(pa1, ca1, blendWeight);
        float4 r2_0 = lerp(pa2, ca2, blendWeight);

        LoadBlendedBoneRows(bi1, prevFrame, prevFrameNext, prevFrameLerp, pb0, pb1, pb2);
        LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, cb0, cb1, cb2);
        float4 r0_1 = lerp(pb0, cb0, blendWeight);
        float4 r1_1 = lerp(pb1, cb1, blendWeight);
        float4 r2_1 = lerp(pb2, cb2, blendWeight);

        posOS = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
        nrmOS = normalize(SkinDir(r0_0, r1_0, r2_0, nrmOS) * w0 + SkinDir(r0_1, r1_1, r2_1, nrmOS) * w1);
    }
}

void ApplyGPUAnimationFullBlend(inout float3 posOS, inout float3 nrmOS, inout float4 tanOS, float4 boneData,
                                 uint frame, uint frameNext, float frameLerp,
                                 uint prevFrame, uint prevFrameNext, float prevFrameLerp,
                                 float blendWeight)
{
    uint bi0 = (uint)boneData.x;
    uint bi1 = (uint)boneData.y;
    float w0 = boneData.z;
    float w1 = boneData.w;

    UNITY_BRANCH
    if (blendWeight >= 1.0)
    {
        float4 r0_0, r1_0, r2_0, r0_1, r1_1, r2_1;
        LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, r0_0, r1_0, r2_0);
        LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, r0_1, r1_1, r2_1);

        posOS = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
        nrmOS = normalize(SkinDir(r0_0, r1_0, r2_0, nrmOS) * w0 + SkinDir(r0_1, r1_1, r2_1, nrmOS) * w1);
        tanOS.xyz = normalize(SkinDir(r0_0, r1_0, r2_0, tanOS.xyz) * w0 + SkinDir(r0_1, r1_1, r2_1, tanOS.xyz) * w1);
    }
    else
    {
        float4 pa0, pa1, pa2, ca0, ca1, ca2;
        float4 pb0, pb1, pb2, cb0, cb1, cb2;

        LoadBlendedBoneRows(bi0, prevFrame, prevFrameNext, prevFrameLerp, pa0, pa1, pa2);
        LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, ca0, ca1, ca2);
        float4 r0_0 = lerp(pa0, ca0, blendWeight);
        float4 r1_0 = lerp(pa1, ca1, blendWeight);
        float4 r2_0 = lerp(pa2, ca2, blendWeight);

        LoadBlendedBoneRows(bi1, prevFrame, prevFrameNext, prevFrameLerp, pb0, pb1, pb2);
        LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, cb0, cb1, cb2);
        float4 r0_1 = lerp(pb0, cb0, blendWeight);
        float4 r1_1 = lerp(pb1, cb1, blendWeight);
        float4 r2_1 = lerp(pb2, cb2, blendWeight);

        posOS = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
        nrmOS = normalize(SkinDir(r0_0, r1_0, r2_0, nrmOS) * w0 + SkinDir(r0_1, r1_1, r2_1, nrmOS) * w1);
        tanOS.xyz = normalize(SkinDir(r0_0, r1_0, r2_0, tanOS.xyz) * w0 + SkinDir(r0_1, r1_1, r2_1, tanOS.xyz) * w1);
    }
}

void ApplyGPUAnimationBlendPosOnly(inout float3 posOS, float4 boneData,
                                    uint frame, uint frameNext, float frameLerp,
                                    uint prevFrame, uint prevFrameNext, float prevFrameLerp,
                                    float blendWeight)
{
    uint bi0 = (uint)boneData.x;
    uint bi1 = (uint)boneData.y;
    float w0 = boneData.z;
    float w1 = boneData.w;

    UNITY_BRANCH
    if (blendWeight >= 1.0)
    {
        float4 r0_0, r1_0, r2_0, r0_1, r1_1, r2_1;
        LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, r0_0, r1_0, r2_0);
        LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, r0_1, r1_1, r2_1);

        posOS = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
    }
    else
    {
        float4 pa0, pa1, pa2, ca0, ca1, ca2;
        float4 pb0, pb1, pb2, cb0, cb1, cb2;

        LoadBlendedBoneRows(bi0, prevFrame, prevFrameNext, prevFrameLerp, pa0, pa1, pa2);
        LoadBlendedBoneRows(bi0, frame, frameNext, frameLerp, ca0, ca1, ca2);
        float4 r0_0 = lerp(pa0, ca0, blendWeight);
        float4 r1_0 = lerp(pa1, ca1, blendWeight);
        float4 r2_0 = lerp(pa2, ca2, blendWeight);

        LoadBlendedBoneRows(bi1, prevFrame, prevFrameNext, prevFrameLerp, pb0, pb1, pb2);
        LoadBlendedBoneRows(bi1, frame, frameNext, frameLerp, cb0, cb1, cb2);
        float4 r0_1 = lerp(pb0, cb0, blendWeight);
        float4 r1_1 = lerp(pb1, cb1, blendWeight);
        float4 r2_1 = lerp(pb2, cb2, blendWeight);

        posOS = SkinPos(r0_0, r1_0, r2_0, posOS) * w0 + SkinPos(r0_1, r1_1, r2_1, posOS) * w1;
    }
}

#endif
