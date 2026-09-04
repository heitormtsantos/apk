Shader "Hidden/RavenDrop/ScopeLensUI"
{
    Properties { _MainTex ("Scope", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float radius = distance(i.uv, float2(0.5, 0.5));
                clip(0.5 - radius);
                fixed4 color = tex2D(_MainTex, i.uv);
                color.a *= smoothstep(0.5, 0.475, radius);
                return color;
            }
            ENDCG
        }
    }
}
