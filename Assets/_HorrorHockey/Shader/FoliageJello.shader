Shader "HorrorHockey/Location/Tree"
{
    Properties
    {
        _BaseMap ("Folliage Texture", 2D) = "white" {}
        _BaseColor ("Foliage Color", Color) = (1, 1, 1, 1)
        _Rotation ("Rotation", Range(-180, 180)) = 0
    }

    SubShader
    {
        Tags
        {

        }

        Pass
        {
            HLSLPROGRAM

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float _Rotation;
            float4 _BaseColor;
            float2 _Pivot;
            CBUFFER_END

            ENDHLSL
        }
    }
}
