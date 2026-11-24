sampler2D _MotionMask;
float _LandMotionCaptureEnabled;
float _LandMotionCapturePhase;

fixed4 SampleLandMotion(sampler2D art, float2 uv)
{
    float4 weights = tex2D(_MotionMask, uv);
    float cycle = _LandMotionCaptureEnabled > .5 ? _LandMotionCapturePhase : _Time.y / 6.0;
    float phase = 6.283185307 * frac(cycle);

    float wave = uv.y * 32.0 + uv.x * 7.0 - phase * 2.0;
    float2 waterTravel = float2(.011 * cos(wave), .012 * sin(wave));
    waterTravel.y += .0025 * sin(wave * 2.0 + uv.x * 11.0);

    float wind = uv.x * 12.0 + uv.y * 2.5 - phase;
    float sway = sin(wind) + .24 * sin(phase * 2.0 - uv.y * 17.0);
    float2 leafTravel = float2(.0125 * sway, .0025 * sin(wind * 2.0));

    float grainWave = uv.x * 24.0 - uv.y * 8.0 - phase * 2.0;
    float lean = sin(grainWave) + .18 * sin(grainWave * 2.0);
    float2 grainTravel = float2(.0115 * lean, -.0042 * lean * lean + .0015 * sin(grainWave));
    float drift = uv.x * 9.0 + uv.y * 6.0 - phase;
    float2 cloudTravel = float2(.013 * sin(drift), .006 * cos(drift));
    float2 travel = weights.r * waterTravel + weights.g * leafTravel
                  + weights.b * cloudTravel + (1.0 - weights.a) * grainTravel;
    return tex2D(art, uv - travel);
}

