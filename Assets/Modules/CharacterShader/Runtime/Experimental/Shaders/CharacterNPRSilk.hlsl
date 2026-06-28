#ifndef CHARACTER_NPR_SILK_INCLUDED
#define CHARACTER_NPR_SILK_INCLUDED

// Calculates the modified albedo color for Silk/Stocking materials (Material ID 5)
// Simulates inverse-fresnel transparency where glancing angles stack up nylon fibers
// and facing angles stretch them out to reveal the skin underneath.
half3 ApplySilkAlbedo(
    half3 originalAlbedo, 
    half3 normalWS, 
    half3 viewDirWS, 
    half4 skinColor, 
    half4 darkColor, 
    half4 lightColor, 
    half transparency, 
    half fresnelPower)
{
    // Calculate Fresnel based on Normal and View Direction
    half NdotV = saturate(dot(normalWS, viewDirWS));
    
    // Inverse Fresnel: 1 at the edges, 0 at the center
    half edgeFresnel = pow(1.0h - NdotV, fresnelPower);
    
    // Center is purely skin tinted by the "light" nylon color
    half3 centerColor = skinColor.rgb * lightColor.rgb;
    
    // Edge is the dense, opaque "dark" nylon color
    half3 edgeColor = darkColor.rgb;
    
    // Blend based on the view angle (fresnel)
    half3 silkColor = lerp(centerColor, edgeColor, edgeFresnel);
    
    // Blend with the original underlying albedo based on Denier (Transparency)
    // A high transparency (sheer stocking) retains more of the original skin/albedo
    // A low transparency (thick tight) overwrites the albedo completely with silk color
    return lerp(originalAlbedo, silkColor, transparency);
}

#endif // CHARACTER_NPR_SILK_INCLUDED
