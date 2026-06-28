#ifndef CHARACTER_NPR_FACE_SDF_INCLUDED
#define CHARACTER_NPR_FACE_SDF_INCLUDED

// Calculates the face shadow based on a Signed Distance Field (Threshold) map.
// This requires the Head Bone's Forward and Right vectors to be provided via C# script.
half CalculateFaceSDFShadow(
    half2 uv, 
    half3 lightDirWS, 
    half3 headForwardWS, 
    half3 headRightWS,
    half shadowOffset,
    half shadowSoftness,
    half mirrorStrength)
{
    // Flatten light direction onto the Head's local XZ plane (ignore Y to avoid vertical shadow distortion)
    half3 flattenedLight = lightDirWS - dot(lightDirWS, half3(0, 1, 0)) * half3(0, 1, 0);
    flattenedLight = dot(flattenedLight, flattenedLight) > 0.0001h ? normalize(flattenedLight) : headForwardWS;
    
    // Fallback if vectors are not assigned by script, we use world forward/right
    // But typically they should be assigned.
    if (length(headForwardWS) < 0.1h) {
        headForwardWS = half3(0, 0, 1);
        headRightWS = half3(1, 0, 0);
    }
    
    headForwardWS = normalize(headForwardWS);
    headRightWS = normalize(headRightWS);

    // Project the light direction onto the head's local axes
    half forwardDot = dot(headForwardWS, flattenedLight);
    half rightDot = dot(headRightWS, flattenedLight);
    
    half sdfValue = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, uv).r;
    half mirroredSdfValue = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, half2(1.0h - uv.x, uv.y)).r;
    half sideMask = step(rightDot, 0.0h);
    sdfValue = lerp(sdfValue, lerp(sdfValue, mirroredSdfValue, sideMask), mirrorStrength);

    // Angle interpolation: 1 = Front, -1 = Back.
    // Map to 0 (Back) to 1 (Front)
    half lightAngle01 = forwardDot * 0.5h + 0.5h;
    
    // If light is from behind (forwardDot < 0), face is fully in shadow.
    // But we map the threshold.
    // lightAngle01 represents how "forward" the light is.
    // If lightAngle01 > sdfValue, the pixel is lit.
    
    // Apply offset
    half threshold = saturate(sdfValue + shadowOffset);
    
    // Soft thresholding for the cel-shaded look
    half faceShadow = smoothstep(threshold - shadowSoftness, threshold + shadowSoftness, lightAngle01);
    
    // If light is behind the head, force shadow
    if (forwardDot < -0.1h) 
    {
        faceShadow = 0.0h;
    }

    return faceShadow;
}

#endif // CHARACTER_NPR_FACE_SDF_INCLUDED
