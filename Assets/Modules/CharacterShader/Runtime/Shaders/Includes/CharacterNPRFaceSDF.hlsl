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
    half shadowSoftness)
{
    // Flatten light direction onto the Head's local XZ plane (ignore Y to avoid vertical shadow distortion)
    half3 flattenedLight = normalize(lightDirWS - dot(lightDirWS, half3(0, 1, 0)) * half3(0, 1, 0));
    
    // Fallback if vectors are not assigned by script, we use world forward/right
    // But typically they should be assigned.
    if (length(headForwardWS) < 0.1h) {
        headForwardWS = half3(0, 0, 1);
        headRightWS = half3(1, 0, 0);
    }
    
    // Project the light direction onto the head's local axes
    half forwardDot = dot(headForwardWS, flattenedLight);
    half rightDot = dot(headRightWS, flattenedLight);
    
    // Calculate the light yaw angle in the range [-1, 1]
    // 0 is perfectly front. 1 is perfectly right. -1 is perfectly left.
    // wait, we just need the dot products to know where the light is.
    // Let's use rightDot to determine left/right direction, and forwardDot to determine front/back.
    
    // A standard Anime SDF map (e.g., Genshin style) usually encodes the shadow threshold in the R channel.
    // The texture represents the "front" of the face.
    // As the light moves to the side, the threshold determines if the pixel falls into shadow.
    half sdfValue = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, uv).r;
    
    // Calculate light angle mapped to [0, 1] for threshold comparison.
    // We can use acos to get a linear angle, or just use the dot product directly.
    // Let's use acos for linear interpolation of shadows.
    // rightDot is between -1 (Left) and 1 (Right). 
    // We want the absolute angle from the forward direction to compare against the SDF.
    // wait, if light is from the left or right, the face has different shadows? Usually anime faces are symmetric,
    // so we can just use the absolute value of the right-dot, or we flip the UV.x if the light is from the other side.
    // Let's assume the SDF map is symmetric and single-channel.
    
    // Determine which side the light is coming from
    bool isLightLeft = rightDot < 0;
    
    // If the light is from the left, we flip the UV.x to sample the shadow symmetrically!
    // This allows a single half-face SDF texture or a full-face texture to work correctly.
    // Actually, most games bake the SDF symmetrically into the texture itself, so no need to flip UV.
    // Let's assume a full-face symmetric SDF map where white=always lit, black=always shadow.
    
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
