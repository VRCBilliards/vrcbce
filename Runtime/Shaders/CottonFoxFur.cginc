// Custom shader made by Juice...
#pragma target 3.0
#include "Lighting.cginc"
#include "UnityCG.cginc"

float4 _Color;
float _Emission;
float _Hue;
float _Sat;
float _Invert;
float _Specular;
float _DL;
float _LP;
float _Gloss;
sampler2D _MainTex, _EmissionMask, _FurMap;
float4 _MainTex_ST, _EmissionMask_ST, _FurMap_ST;
float _Length;
float _Density;
float _Shading;

// HSV Controls
float alt(float n, float3 hsv) {
	float k=fmod(n+hsv.x/60,6);
	return (hsv.z-hsv.z*hsv.y*max(0,min(min(k,4-k),1)));
}
float3 conv(float3 clr) {
	float mx=max(clr.r,max(clr.g,clr.b));
	float mn=min(clr.r,min(clr.g,clr.b));
	float3 hsv={0,0,mx};
	float c=mx-mn;
	if(hsv.z == clr.r){
		hsv.x=60*((clr.g-clr.b)/c);
	}else{
		if(hsv.z == clr.g){
			hsv.x=60*(2+(clr.b-clr.r)/c);
		}else{
			if(hsv.z == clr.b){
				hsv.x=60*(4+(clr.r-clr.g)/c);
			}
		}
	}
	if(hsv.z!=0){
		hsv.y=c/hsv.z;
	}
	hsv.x+=_Hue;
	if(hsv.x>360)hsv.x-=360;
	hsv.y*=_Sat;
	return float3(alt(5,hsv),alt(3,hsv),alt(1,hsv));
}

struct appdata {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 pos: POSITION;
    float2 uv: TEXCOORD0;
    float2 uv2: TEXCOORD1;
    float2 uv3: TEXCOORD2;
    float2 uv4: TEXCOORD3;
};

struct v2f {
    float4 pos: SV_POSITION;
    float2 uv: TEXCOORD0;
    float2 uv2: TEXCOORD1;
    float2 uv3: TEXCOORD2;
    float2 uv4: TEXCOORD3;
    float3 worldNormal: NORMAL;
    float3 worldPos: TEXCOORD4;
};

v2f vert (appdata v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.pos);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.uv2 = TRANSFORM_TEX(v.uv2, _FurMap);
    o.uv3 = TRANSFORM_TEX(v.uv3, _EmissionMask);
    o.uv4 = v.uv4;
    return o;
}

v2f vert_surface(appdata v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.uv3 = TRANSFORM_TEX(v.uv3, _EmissionMask);
    o.uv4 = v.uv4;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    return o;
}

v2f vert_base(appdata v) {
    v2f o;
    float3 P = v.vertex.xyz + v.normal * ((_Length) / 10) * FURSTEP;
    o.pos = UnityObjectToClipPos(float4(P, 1.0));
    o.uv = TRANSFORM_TEX(v.uv, _MainTex );
    o.uv2 = TRANSFORM_TEX(v.uv2, _FurMap);
    o.uv3 = TRANSFORM_TEX(v.uv3, _EmissionMask);
    o.uv4 = v.uv4;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    return o;
}

// Reflectivity
float3 reflection(float3 UVworldNormal, float3 UVworldPos, float Gloss) {
    float3 worldNormal = normalize(UVworldNormal);
    float3 worldLight = normalize(_WorldSpaceLightPos0.xyz);
    float3 worldView = normalize(_WorldSpaceCameraPos.xyz - UVworldPos.xyz);
    float3 worldHalf = normalize(worldView + worldLight);
    float3 worldViewDir = normalize(UnityWorldSpaceViewDir(UVworldPos));
    float3 worldRefl = reflect(-worldViewDir, UVworldNormal);
    float4 reflectionData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldRefl);
    float3 reflectionColor = DecodeHDR (reflectionData, unity_SpecCube0_HDR);
    float4 r = 0;
    return reflectionColor * Gloss;
}

// Color & Texture
float3 surface(float3 UVworldNormal, float3 UVworldPos, float3 UVpos, float2 UVc, float2 UVm) {
    float3 worldNormal = normalize(UVworldNormal);
    float3 worldLight = normalize(_WorldSpaceLightPos0.xyz);
    float3 worldView = normalize(_WorldSpaceCameraPos.xyz - UVworldPos.xyz);
    float3 worldHalf = normalize(worldView + worldLight);
    float3 lightprobes = (ShadeSH9(float4(UVworldNormal,1)) * _LP * 3);
    float3 albedo = abs(_Invert - conv(tex2D(_MainTex, UVc).rgb) * _Color);
    float3 emission = (albedo * tex2D(_EmissionMask, UVm).rgb) * ((_Emission * 2) / 2);
    albedo -= (pow(1 - FURSTEP, 2)) * _Shading;
    float3 ambient = emission + albedo * (UNITY_LIGHTMODEL_AMBIENT.xyz) * _DL;
    float3 diffuse = _LightColor0.rgb * albedo * (saturate(dot(worldNormal, worldLight)) * _DL);
    fixed3 specular = _LightColor0.rgb * diffuse * pow(saturate(dot(worldNormal, worldHalf)), clamp( (_Specular * 90), .001, 90) );

    return ambient + diffuse + specular;
}

// Mesh Surface
float4 frag_surface(v2f i): SV_Target {
    float3 r = reflection(i.worldNormal, i.worldPos, _Gloss);
    float3 color = surface(i.worldNormal, i.worldPos, i.pos, i.uv, i.uv3);
    return float4(color + r, 1);
}

// Shell Layers
float4 frag_base(v2f i): SV_Target {
    float3 r = reflection(i.worldNormal, i.worldPos, _Gloss);
    float3 color = surface(i.worldNormal, i.worldPos, i.pos, i.uv, i.uv3);
    float3 noise = tex2D(_FurMap, i.uv2).rgb;
    float alpha = clamp(noise - (FURSTEP * FURSTEP) * (_Density), 0, 1);
    return float4(color + r, alpha);
}