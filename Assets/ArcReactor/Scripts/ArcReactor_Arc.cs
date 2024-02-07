using UnityEngine;
using System.Collections;
using System.Linq;
using System;

[AddComponentMenu("Arc Reactor Rays/Ray System")]
public class ArcReactor_Arc : MonoBehaviour
{
	[Header("Individual arcs settings")]
	[Tooltip("Main visualisation settings is here.")]
    public LineRendererInfo[] arcs;    
	[Header("Playback")]
	[Tooltip("Lifetime of this system. Used as cycle length in cyclis playback types.")]
    public float lifetime = 1;
	[Tooltip("See manual for descriptions of playback types")]
    public ArcsPlaybackType playbackType = ArcsPlaybackType.once;
	[Tooltip("How far this system progressed along its lifetime/cycle.")]
	public float elapsedTime = 0;
	[Tooltip("Elapsed time will decrease as time progresses. Usually set automatically depending on playback type.")]
	public bool playBackward = false;
	[Tooltip("Freezes elapsed time from progressing. Spatial noise and oscillations are unaffected. ")]
	public bool freeze = false;
	[Tooltip("If true, SendMessage will be called on MessageReciever with this component as parameter after any playback event (reached end of its lifetime, reached bounce during ping-pong playback etc.)")]
    public bool playbackMessages = false;
    public GameObject messageReciever;
	[Header("Shape and size")]
	[Tooltip("Vertices of main shape of this ray system. Lower priority than Shape Transforms.")]
	public Vector3[] shapePoints;
	[Tooltip("Transforms that work as vertices of main shape of this ray system. Higher priority than Shape Points.")]
	public Transform[] shapeTransforms;
	[Tooltip("Loops shape from last point/transform to first")]
	public bool closedShape;
	[Tooltip("Float that affects all parameters determining general size of ray system. Affects linerenderer width, spatial noise and oscillation amplitudes, particle emission volume and lights radius.")]
    public float sizeMultiplier = 1;
    public InterpolationType interpolation = InterpolationType.CatmullRom_Splines;
	[Tooltip("Start and end of rays will gradually decrease amplitude of spatial noise and oscillations")]
    public EaseInOutOptions easeInOutOptions;
	[Tooltip("Use this for Ghostbusters-style flexible rays")]
	public bool tangentsFromTransforms;
	public float tangentsFromTransformsPower = 0.25f;
	[Tooltip("If flag is set to true at specific index, transform from ShapeTransforms array with that index will be destroyed when this component is destroyed.")]
    public bool[] transformsDestructionFlags;

	[Header("Global oscillation parameters")]
	[Tooltip("Trigonometric calculations use this normal as default osciallation plane normal.")]
    public Vector3 oscillationNormal = Vector3.up;
    public bool localSpaceOcillations = false;

	[Header("Miscellaneous")]
	[Tooltip("If shape length increases or decreases by this much, re-initializes arcs - mostly recalculates lights and segments.")]
    public float reinitThreshold = 0.5f;
	[Tooltip("Used only if ArcReactor_Manager is present")]
    public int performancePriority = 0;
	public ArcReactorSingleLayer linerendererLayer;
	[Tooltip("If null, mainCamera will be used")]
	public Camera currentCamera;

	[Header("Custom sorting")]
    public bool customSorting;
	public string sortingLayerName;
    public int sortingOrder;

	[System.NonSerialized]
	public bool currentlyInPool;

	const int maxCalcDetalization = 10;

	protected SpatialInfo[] resultingShape;

    protected int oldShapeTransformsSize = 0;
    protected float overlap = 0;
    protected float[] noiseOffsets;
    protected float[] noiseScale;
    protected Vector3[,] arcPoints;
    protected Vector3[,] shiftVectors;
    protected Vector3[,] arcTangents;
    protected Quaternion[,] arcTangentsShift;
    protected Vector3[] shapeTangents;
    protected Vector3[][] vertices;
    protected Vector3[][] oldVertices;
	protected ParticleSystem.Particle[][][] particleBuffers;
    protected Transform[,] lightsTransforms;
    protected Light[,] lights;
    protected LineRenderer[] lrends;
    protected int[] segmNums;
    protected int[] vertexCount;
    protected int[] oldVertexCount;
    protected int[] lightsCount;
    protected float shapeLength;
    protected float oldShapeLength;
    protected float[] shapeKeyLocations;
    protected float[] shapeKeyNormalizedLocations;
    protected float[] maxStartWidth;
    protected float[] maxEndWidth;
    protected float[] coreCoefs;
    protected Vector3 oscNormal;
    protected LensFlare startFlare;
    protected LensFlare endFlare;
    //protected Mesh[][] emitterMeshes;
    protected ParticleSystem[][] emitterSystems;
    protected ArcReactor_EmitterDestructor[][] emitterDestructors;

    public float ShapeLength
    {
        get
        {
            return shapeLength;
        }
    }

    public int PerformancePriority
    {
        get
        {
            return performancePriority;
        }
    }

    public enum PropagationType
    {		
        instant = 0,
        globalSpaceSpeed = 1,
        localTimeCurve = 2
    }

    public enum ArcsPlaybackType
    {
        once = 0,
        loop = 1,
        pingpong = 2,
        clamp = 3,
		pingpong_once = 4,
		pingpong_clamp_once = 5
    }

    public enum InterpolationType
    {
        CatmullRom_Splines = 0,
        Linear = 1
    }

    public enum SpatialNoiseType
    {
        TangentRandomization = 0,
        CubicRandomization = 1,
        BrokenTangentRandomization = 2
    }

    public enum OscillationType
    {
        sine_wave = 0,
        rectangular = 1,
        zigzag = 2
    }

    public enum FadeTypes
    {
        none = 0,
        worldspacePoint = 1,
        relativePoint = 2
    }

	public enum ShapeTypes
	{
		start_only = 0,
		start_end = 1,
		start_curve_as_shape = 2
	}

	[System.Serializable]
	public struct SpatialInfo
	{
		public Vector3 position;
		public Vector3 tangent;
	}
		

    [System.Serializable]
    public class ArcNestingOptions
    {
		[Tooltip("If true, this arc will follow shape of another arc, instead of global shape defined by ShapePoints or ShapeTransforms")]
        public bool Nested = false;
		[Tooltip("Index of arc that will define shape")]
        public int parentArcIndex = 0;
		[Tooltip("If true, this arc will follow shape defined by interpolating two arcs.")]
        public bool combinedNesting = false;
        public int secondaryArcIndex = 0;
		[Tooltip("How much second arc affects resulting shape (0 = no effect, 1 = full effect)")]
        public float nestingCoef = 0;
    }


    [System.Serializable]
    public class EaseInOutOptions
    {
		[Tooltip("Start and end stretches of this arc will gradually be affected by oscillation and spatial randomization less and less.")]
        public bool useEaseInOut;
		[Tooltip("Curve that defines ease in effect. Horizontal axis defines length of the arc(clamped by distance), vertical axis - oscillation and randomization strength.")]
        public AnimationCurve easeInOutCurve;
		[Tooltip("Length of affected arc stretches.")]
        public float distance;
    }

    [System.Serializable]
    public class ArcPropagationOptions
    {
		[Tooltip("How arc will propagate through its defined length.\r\nInstant - it will spawn at full length.\r\nGlobal speed - it will move along its defined shape at fixed speed.\r\nLocal time curve - it will change it's visible length following timeCurve.")]
		public PropagationType propagationType = PropagationType.instant;
        public float globalSpeed = 1.0f;
		[Tooltip("Horizontal axis - lifetime, Vertical axis - visible length between 0 and 1")]
        public AnimationCurve timeCurve;
    }

    [System.Serializable]
    public class ArcColorOptions
    {
		[Tooltip("Color gradient defining color of the front of this arc(or of the whole arc if onlyStartColor set to true). Horizontal axis is lifetime of this arc.")]
        public Gradient startColor;
		[Tooltip("If true, startColor will define color of the whole arc.")]
        public bool onlyStartColor = true;
		[Tooltip("Color gradient defining color of the back of this arc. Horizontal axis is lifetime of this arc.")]
        public Gradient endColor;
		[Tooltip("Color gradient defining core color. Horizontal axis is lifetime of this arc. More info about core coloring in the manual.")]
        public Gradient coreColor;
		[Tooltip("Curve defining core coloring strength. Horizontal axis is lifetime of this arc, vertical axis is core strength. More info about core coloring in the manual.")]
        public AnimationCurve coreCurve;
		[Tooltip("Coefficient for core coloring unstability through lifetime. More info about core coloring in the manual.")]
        public float coreJitter;
		[Tooltip("Gradually fades end of this arc to invisible.\r\nNone - no effect.\r\nRelative point - fade point will define length of fading between 0 and 1 as a fraction of total length of this arc.\r\nWorld space point - fade point will define length of fading in Unity distance units.")]
		public FadeTypes fade = FadeTypes.none;
        public float fadePoint;
		[Tooltip("Gradually fades start of this arc to invisible.\r\nNone - no effect.\r\nRelative point - fade point will define length of fading between 0 and 1 as a fraction of total length of this arc.\r\nWorld space point - fade point will define length of fading in Unity distance units.")]
        public FadeTypes frontFade;
        public float frontFadePoint;
    }

    [System.Serializable]
    public class ArcSizeOptions
    {
		[Tooltip("Type of arc shape interpolation. Note that you can mix global interpolation and arc interpolation types (for example, global linear and local splines for soft reflections).")]
        public InterpolationType interpolation = InterpolationType.CatmullRom_Splines;
		[Tooltip("Defines width of this arc along its length.\r\nStart_only - fixed width along length, startWidthCurve defines width through lifetime.\r\nStart_end - gradient width along length, start and end curves define widths along lifetime.\r\nStart_curve_as_shape - fixed width along lifetime, startWidthCurve defines width along length.")]
		public ShapeTypes shapeType = ShapeTypes.start_only;
        public AnimationCurve startWidthCurve;
        //public bool onlyStartWidth = true;
        public AnimationCurve endWidthCurve;
		[Tooltip("Arc will be divided by segments, which are affected by all settings.")]
        public float segmentLength = 10;
		[Tooltip("If true, closest segment to ShapePoint or ShapeTransform will be moved to its exact position. Used in hard laser-like reflections.")]
        public bool snapSegmentsToShape = false;
		[Tooltip("Each segment will be divided by smoothing sub-segments, which are unaffected by spatial noise. Increase to improve vertex count without making spatial noise more frequent.")]
        public int numberOfSmoothingSegments = 0;
		[Tooltip("Minimum number of segments this arc will contain.")]
        public int minNumberOfSegments = 1;
		[Tooltip("Set this to a value greater than 0, to get rounded corners between each segment of the line (LineRenderer parameter).")]
		public int cornerVertices = 0;
		[Tooltip("Set this to a value greater than 0, to get rounded corners on each end of the line (LineRenderer parameter).")]
		public int capVectices = 0;
    }

    [System.Serializable]
    public class TextureAnimationOptions
    {
		[Tooltip("Main texture of this arc. Horizontal axis - width of the arc, Vertical axis - length of the arc.")]
        public Texture shapeTexture;
		[Tooltip("Noise texture will be added or substracted to main texture depending on noiseCoef curve. It can also move during playback along arc length.")]
        public Texture noiseTexture;
		[Tooltip("Strength of noise to add(positive value) or substract(negative) to main texture.\r\nHorizontal axis - lifetime.\r\nVertical axis - noise strength.")]
        public AnimationCurve noiseCoef;
		[Tooltip("If true, will move noise along length of the arc.")]
        public bool animateTexture;
		[Tooltip("Tiling of the noise texture along arc length.")]
        public float tileSize;
		[Tooltip("Speed of noise movement if animateTexture set to true.")]
        public float noiseSpeed;
        //public float noisePower;
    }

    [System.Serializable]
    public class ArcSpatialNoiseOptions
    {
		[Tooltip("CubicRandomization - segments will gradually move away from their calculated positions.\r\nTangentRandomization - tangents will gradually move away from default while preserving flatness.\r\nBrokenTangentRandomization - same as TangentRandomization, but without concern for preserving flat tangents(useful for electrical/zagged rays).")]
		public SpatialNoiseType type = SpatialNoiseType.BrokenTangentRandomization;
		[Tooltip("For CubicRandomization - initial deviation from calculated position in Unity length units.\r\nFor both tangent randomizations - initial deviation in degrees.")]
        public float scale = 0;
		[Tooltip("Gradual change power during playback (in Unity units or degrees).")]
        public float scaleMovement = 0;
		[Tooltip("Limits segment position deviation during playback inside cube with this size.")]
		public float scaleLimit = 1;
		[Tooltip("How frequent (in seconds), on average, spatial noise will reset itself. 0 - no resetting.")]
        public float resetFrequency = 0;
		[Tooltip("Perfromance manager parameter. Read manual for details.")]
        public int invisiblePriority;
    }

    [System.Serializable]
    public class ArcLightsOptions
    {
		[Tooltip("If true, point lights will be generated along arc length for simulating volumetric light source.")]
        public bool lights = false;
		[Tooltip("")]
        public float lightsRange = 5;
		[Tooltip("")]
        public float lightsIntensityMultiplyer = 5;
		[Tooltip("")]
        public LightRenderMode renderMode = LightRenderMode.Auto;
		[Tooltip("Perfromance manager parameter. Read manual for details.")]
        public int priority;
    }

    [System.Serializable]
    public class OscillationInfo
    {
		[Tooltip("Defines waveform of oscillation.")]
		public OscillationType type = OscillationType.sine_wave;
		[Tooltip("Allows 3-dimensional spiral to form based on oscillation parameters. Duplicates current oscillation with phase and plane rotation rotated 90 degrees. Only sine_wave gives spiral effect.")]
        public bool swirl = false;
		[Tooltip("Rotation of arc oscillation plane around default oscillation plane. See manual for illustration.")]
        public float planeRotation;
		[Tooltip("")]
        public float wavelength;
		[Tooltip("Wavelength will be changed to closest wavelength that allows for integer number of periods along system shape. This comes in handy on closed shape/looped systems.")]
        public bool integerPeriods;
		[Tooltip("GlobalSpace – wavelength set in unity units\r\nLocalSpace – wavelength set in parts of system shape length(e.g, wavelength = 0.2 will set wavelength to 1/5 of overall system length)")]
        public WavelengthMetric metric = WavelengthMetric.globalSpace;
		[Tooltip("Maximum deviation of oscillating arc from its original shape.")]
        public float amplitude;
		[Tooltip("Period shift of oscillation")]
        public float phase;
		[Tooltip("Lets you animate oscillation by moving wave periods along arc.")]
        public float phaseMovementSpeed;
		[Tooltip("Perfromance manager parameter. Read manual for details.")]
        public int invisiblePriority;
    }

    [System.Serializable]
    public class ParticleEmissionOptions
    {
		[Tooltip("If true, will emit particles.")]
        public bool emit = false;
		[Tooltip("Prefab containing Particle System component.")]
		public ParticleSystem shurikenPrefab;
		[Tooltip("If true, postprones destruction of particle system until all particles die off, otherwise destroys it immedieately on main component destruction.")]
        public bool emitAfterRayDeath = false;
		[Tooltip("How many particles will be generated per Unity unit of shape length (per second)")]
        public float particlesPerMeter = 0;
		[Tooltip("Emission intensity during lifetime of ray.\r\nHorizontal axis - lifetime.\r\nVertical axis - emission multiplier.")]
        public AnimationCurve emissionDuringLifetime;
		[Tooltip("Usually particles are emitted from within ray width.This curve allows affecting emitter width in regards to ray width.\r\nHorizontal axis - lifetime.\r\nVertical axis - width multiplier.")]
        public AnimationCurve radiusCoefDuringLifetime;
		[Tooltip("Direction of particles velocity. 0 - random direction, 1 - along the arc shape, blended inbetween.\r\nHorizontal axis - lifetime.\r\nVertical axis - direction multiplier.")]
        public AnimationCurve directionDuringLifetime;
		[Tooltip("Blend coefficient for determining resulting color of particle.\nBlended colors are native color of particle as determined by particle system and\ncolor of the arc.")]
		public float arcColorInfluence = 0.5f;
    }
		
    public enum WavelengthMetric
    {
        globalSpace = 0,
        localSpace = 1
    }

    [System.Serializable]
    public class ArcFlaresInfo
    {
        public FlareInfo startFlare;
        public FlareInfo endFlare;
        public bool useNoiseMask;
        public AnimationCurve noiseMaskPowerCurve;
    }

    [System.Serializable]
    public class FlareInfo
    {
        public bool enabled = false;
        public Flare flare;
        public float fadeSpeed = 50;
        public float maxBrightness;
        public float maxBrightnessDistance;
        public float minBrightness;
        public float minBrightnessDistance;
        public LayerMask ignoreLayers = (LayerMask)6;
    }

    [System.Serializable]
    public class ShiftCurveInfo
    {
        public AnimationCurve shapeCurve;
        public float curveWidth;
        public float planeRotation;
        public WavelengthMetric metric = WavelengthMetric.globalSpace;
        public float curveLength;
        public bool notAffectedByEaseInOut;
        public int invisiblePriority;
    }

    [System.Serializable]
    public class LineRendererInfo
    {
        public Material material;
        public ArcColorOptions colorOptions;
		[Tooltip("Shape and size options")]
        public ArcSizeOptions sizeOptions;
		[Tooltip("Defines how this ray/arc moves through space")]
        public ArcPropagationOptions propagationOptions;
		[Tooltip("Shuriken particles emission")]
        public ParticleEmissionOptions[] emissionOptions;
		[Tooltip("Randomizes shape of this ray/arc")]
        public ArcSpatialNoiseOptions[] spatialNoise;
        public TextureAnimationOptions textureOptions;
        public ArcLightsOptions lightsOptions;
        public ArcFlaresInfo flaresOptions;
		[Tooltip("Allows ray/arc to follow shape of other ray/arc")]
        public ArcNestingOptions nesting;
        public OscillationInfo[] oscillations;
        public ShiftCurveInfo[] shapeCurves;
    }


	protected static float VectorFlatLength(Vector3 vector)
	{
		return (Mathf.Abs(vector.x) + Mathf.Abs(vector.y) + Mathf.Abs(vector.z));
	}

    public static Vector3 HermiteCurvePoint(float t, Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1)
    {
        float tsq = t * t;
        float tcub = t * t * t;
        return (2 * tcub - 3 * tsq + 1) * p0
                + (tcub - 2 * tsq + t) * m0
                + (-2 * tcub + 3 * tsq) * p1
                + (tcub - tsq) * m1;
    }

    public void FillResultingShape()
    {
        if (resultingShape == null)
			resultingShape = new SpatialInfo[0];
        if (shapePoints != null && shapeTransforms != null)
        {
            if (Mathf.Max(shapeTransforms.Length, shapePoints.Length) != resultingShape.Length)
                Array.Resize(ref resultingShape, Mathf.Max(shapeTransforms.Length, shapePoints.Length));

            for (int i = 0; i < resultingShape.Length; i++)
            {
                if ((shapeTransforms.Length > i) && (shapeTransforms[i] != null))
				{
                    resultingShape[i].position = shapeTransforms[i].position;
					resultingShape[i].tangent = shapeTransforms[i].forward * shapeTransforms[i].localScale.x;
				}
                else
                    resultingShape[i].position = shapePoints[i];
            }
        }
        else if (shapeTransforms != null)
        {
            if (shapeTransforms.Length != resultingShape.Length)
                Array.Resize(ref resultingShape, shapeTransforms.Length);

            for (int i = 0; i < resultingShape.Length; i++)
                resultingShape[i].position = shapeTransforms[i].position;
        }
        else if (shapePoints != null)
        {
            if (shapePoints.Length != resultingShape.Length)
                Array.Resize(ref resultingShape, shapePoints.Length);

            for (int i = 0; i < resultingShape.Length; i++)
                resultingShape[i].position = shapePoints[i];
        }
    }

	    
    public void SetPerformancePriority(int newPriority)
    {
        if (lightsCount != null && performancePriority != newPriority)
        {
            performancePriority = newPriority;
            for (int n = 0; n < arcs.Length; n++)
            {
                if (arcs[n].lightsOptions.lights && (lightsCount[n] > 0))
                {
                    for (int i = 0; i < lightsCount[n]; i++)
                        lights[n, i].enabled = arcs[n].lightsOptions.priority <= performancePriority;
                }
            }
        }
    }

    protected Vector3 CalculateCurveShift(Vector3 direction, float position, int arcInd)
    {
        Vector3 sumShift = Vector3.zero;
        foreach (ShiftCurveInfo curv in arcs[arcInd].shapeCurves)
        {
            if (lrends[arcInd].isVisible || curv.invisiblePriority <= performancePriority)
            {
                float shift;
                if (curv.metric == WavelengthMetric.localSpace)
                    shift = curv.shapeCurve.Evaluate(position / shapeLength) * curv.curveWidth;
                else
                    shift = curv.shapeCurve.Evaluate(position / curv.curveLength) * curv.curveWidth;

                Quaternion rot;
                rot = Quaternion.AngleAxis(curv.planeRotation, direction);
                Vector3 normal = Vector3.Cross(direction, oscNormal);
                if (curv.notAffectedByEaseInOut)
                    sumShift += rot * normal.normalized * shift;
                else
                    sumShift += rot * normal.normalized * shift * GetShiftCoef(position / shapeLength);
            }
        }
        return sumShift * sizeMultiplier;
    }




    protected Vector3 CalculateOscillationShift(Vector3 direction, float position, int arcInd)
    {
        Vector3 sumShift = Vector3.zero;
        foreach (OscillationInfo osc in arcs[arcInd].oscillations)
        {
            if (lrends[arcInd].isVisible || osc.invisiblePriority <= performancePriority)
            {
                float wavelength = osc.wavelength * sizeMultiplier;
                float effectiveWavelength = wavelength;
                if (osc.integerPeriods && osc.metric == WavelengthMetric.globalSpace)
                    effectiveWavelength = shapeLength / Mathf.Ceil(shapeLength / wavelength);
                if (osc.integerPeriods && osc.metric == WavelengthMetric.localSpace)
                    effectiveWavelength = 1 / Mathf.Ceil(1 / wavelength);
                float angle;
                if (osc.metric == WavelengthMetric.globalSpace)
                    angle = osc.phase * Mathf.Deg2Rad + (position - effectiveWavelength * ((int)(position / effectiveWavelength))) / effectiveWavelength * Mathf.PI * 2;
                else
                    angle = osc.phase * Mathf.Deg2Rad + (position / shapeLength - effectiveWavelength * ((int)(position / shapeLength / effectiveWavelength))) / effectiveWavelength * Mathf.PI * 2;

                float shift;
                switch (osc.type)
                {
                    case OscillationType.sine_wave:
                        shift = osc.amplitude * Mathf.Sin(angle);
                        break;
                    case OscillationType.rectangular:
                        if ((angle * Mathf.Rad2Deg) % 360 > 180)
                            shift = -osc.amplitude;
                        else
                            shift = osc.amplitude;
                        break;
                    case OscillationType.zigzag:
                        shift = osc.amplitude * (Mathf.Abs(((angle * Mathf.Rad2Deg) % 180) / 45 - 2) - 1);
                        break;
                    default:
                        shift = 0;
                        break;
                }
                Quaternion rot;
                rot = Quaternion.AngleAxis(osc.planeRotation, direction);
				Vector3 normal = Vector3.Cross(direction, oscNormal).normalized;
                sumShift += rot * normal * shift;
                if (osc.swirl)
                {
                    if (osc.metric == WavelengthMetric.globalSpace)
                        angle = (osc.phase + 90) * Mathf.Deg2Rad + (position - effectiveWavelength * ((int)(position / effectiveWavelength))) / effectiveWavelength * Mathf.PI * 2;
                    else
                        angle = (osc.phase + 90) * Mathf.Deg2Rad + (position / shapeLength - effectiveWavelength * ((int)(position / shapeLength / effectiveWavelength))) / effectiveWavelength * Mathf.PI * 2;
                    switch (osc.type)
                    {
                        case OscillationType.sine_wave:
                            shift = osc.amplitude * Mathf.Sin(angle);
                            break;
                        case OscillationType.rectangular:
                            if ((angle * Mathf.Rad2Deg) % 360 > 180)
                                shift = -osc.amplitude;
                            else
                                shift = osc.amplitude;
                            break;
                        case OscillationType.zigzag:
                            shift = osc.amplitude * (Mathf.Abs(((angle * Mathf.Rad2Deg) % 180) / 45 - 2) - 1);
                            break;
                        default:
                            shift = 0;
                            break;
                    }
                    rot = Quaternion.AngleAxis(osc.planeRotation + 90, direction);
                    sumShift += rot * normal * shift;
                }
            }
        }
        return sumShift * sizeMultiplier;
    }

	public float GetArcWidthAtPoint(int arc, float point)
	{
		switch (arcs[arc].sizeOptions.shapeType)
		{
		case ShapeTypes.start_only:
			return arcs[arc].sizeOptions.startWidthCurve.Evaluate(elapsedTime/lifetime) * sizeMultiplier;
		case ShapeTypes.start_end:
			return Mathf.Lerp(arcs[arc].sizeOptions.startWidthCurve.Evaluate(elapsedTime/lifetime),arcs[arc].sizeOptions.endWidthCurve.Evaluate(elapsedTime/lifetime),point) * sizeMultiplier;
		case ShapeTypes.start_curve_as_shape:
			return arcs[arc].sizeOptions.startWidthCurve.Evaluate(point) * sizeMultiplier;
		}
		return arcs[arc].sizeOptions.startWidthCurve.Evaluate(elapsedTime/lifetime);
	}


    protected void CalculateShape()
    {
        FillResultingShape();
        if (oldShapeTransformsSize != resultingShape.Length)
        {
            SetShapeArrays();
        }

        if (closedShape)
        {
            shapeLength = 0;

            for (int i = 0; i < resultingShape.Length - 1; i++)
            {
                shapeKeyLocations[i] = shapeLength;
				shapeLength += (resultingShape[i].position - resultingShape[i + 1].position).magnitude;
            }
            shapeKeyLocations[resultingShape.Length - 1] = shapeLength;

			float closeLoopLength = (resultingShape[0].position - resultingShape[resultingShape.Length - 1].position).magnitude;
            shapeLength += closeLoopLength;
            shapeKeyLocations[resultingShape.Length] = shapeLength;

            shapeLength += overlap;
        }
        else
        {
            shapeLength = 0;

            for (int i = 0; i < resultingShape.Length - 1; i++)
            {
                shapeKeyLocations[i] = shapeLength;
				shapeLength += (resultingShape[i].position - resultingShape[i + 1].position).magnitude;
            }
            shapeKeyLocations[resultingShape.Length - 1] = shapeLength;
        }

        for (int i = 0; i < shapeKeyLocations.Length; i++)
            shapeKeyNormalizedLocations[i] = shapeKeyLocations[i] / shapeLength;


		if (tangentsFromTransforms)
		{
			switch (interpolation)
			{
			case InterpolationType.CatmullRom_Splines:
				if (closedShape)
				{
					for (int i = 0; i < resultingShape.Length; i++)
					{						
						shapeTangents[i] = ((resultingShape[AddCyclicShift(i, 1, resultingShape.Length - 1)].position - resultingShape[AddCyclicShift(i, -1, resultingShape.Length - 1)].position) / 2).magnitude
							* resultingShape[i].tangent * tangentsFromTransformsPower;
					}
				}
				else
				{
					shapeTangents[0] = (resultingShape[1].position - resultingShape[0].position).magnitude * resultingShape[0].tangent * tangentsFromTransformsPower;;
					shapeTangents[resultingShape.Length - 1] = (resultingShape[resultingShape.Length - 1].position - resultingShape[resultingShape.Length - 2].position).magnitude * resultingShape[resultingShape.Length - 1].tangent * tangentsFromTransformsPower;;
					for (int i = 1; i < resultingShape.Length - 1; i++)
					{
						shapeTangents[i] = ((resultingShape[i + 1].position - resultingShape[i - 1].position) / 2).magnitude * resultingShape[i].tangent * tangentsFromTransformsPower;
					}
				}
				break;
			}
		}
		else
		{
			switch (interpolation)
			{
			case InterpolationType.CatmullRom_Splines:
				if (closedShape)
				{
					for (int i = 0; i < resultingShape.Length; i++)
					{
						shapeTangents[i] = (resultingShape[AddCyclicShift(i, 1, resultingShape.Length - 1)].position - resultingShape[AddCyclicShift(i, -1, resultingShape.Length - 1)].position) / 2;
					}
				}
				else
				{
					shapeTangents[0] = resultingShape[1].position - resultingShape[0].position;
					shapeTangents[resultingShape.Length - 1] = resultingShape[resultingShape.Length - 1].position - resultingShape[resultingShape.Length - 2].position;
					for (int i = 1; i < resultingShape.Length - 1; i++)
					{
						shapeTangents[i] = (resultingShape[i + 1].position - resultingShape[i - 1].position) / 2;
					}
				}
				break;
			}
		}
        if (oldShapeLength == 0 || Mathf.Abs((oldShapeLength - shapeLength) / shapeLength) > reinitThreshold)
        {
            Initialize();
        }
    }

    protected int AddCyclicShift(int a, int b, int size)
    {
        int s = a + b;
        if (s < 0)
            return s + size + 1;
        if (s > size)
            return s - size - 1;
        return s;
    }

    protected float AddCyclicShift(float a, float b, float size)
    {
        float s = a + b;
        if (s < 0)
            return s + size;
        if (s > size)
            return s - size;
        return s;
    }

    protected Quaternion RandomXYQuaternion(float angle)
    {
        if (angle > 0)
            return Quaternion.Euler(new Vector3(UnityEngine.Random.Range(-angle, angle),
                                                UnityEngine.Random.Range(-angle, angle),
                                                0));
        else
            return Quaternion.identity;
    }

    protected void SetArcShape(int n)
    {
        float overlapCeof = 1 + overlap / shapeLength;
        int closeShapeShift = 1;
        for (int nI = 0; nI < arcs[n].spatialNoise.Length; nI++)
        {
            switch (arcs[n].spatialNoise[nI].type)
            {
                case SpatialNoiseType.CubicRandomization:
                    if (UnityEngine.Random.value > arcs[n].spatialNoise[nI].resetFrequency * Time.deltaTime)
                    {
                        closeShapeShift = 1;
                        if (closedShape)
                            closeShapeShift = 0;
                        for (int i = 0; i < segmNums[n] + closeShapeShift; i++)
                        {
							float step = arcs[n].spatialNoise[nI].scaleMovement * Time.deltaTime * 60 * sizeMultiplier;							
						shiftVectors[n, i] += ( RandomVector3(step) - Mathf.Max(VectorFlatLength(shiftVectors[n, i]) - arcs[n].spatialNoise[nI].scaleLimit/2,0)/arcs[n].spatialNoise[nI].scaleLimit * shiftVectors[n, i].normalized * step) * GetShiftCoef((float)i / segmNums[n]);
                        }
                    }
                    else
                    {
                        ResetArcNoise(n, nI);
                    }
                    break;
                case SpatialNoiseType.TangentRandomization:
                    if (UnityEngine.Random.value > arcs[n].spatialNoise[nI].resetFrequency * Time.deltaTime)
                    {
                        closeShapeShift = 1;
                        if (closedShape)
                            closeShapeShift = 0;
                        for (int i = 0; i < segmNums[n] + closeShapeShift; i++)
                        {
                            arcTangentsShift[n, i * 2] = arcTangentsShift[n, i * 2] * RandomXYQuaternion(arcs[n].spatialNoise[nI].scaleMovement * GetShiftCoef((float)i / segmNums[n]));
                            arcTangentsShift[n, i * 2 + 1] = arcTangentsShift[n, i * 2];
                        }
                    }
                    else
                    {
                        ResetArcNoise(n, nI);
                    }
                    break;
                case SpatialNoiseType.BrokenTangentRandomization:
                    if (UnityEngine.Random.value > arcs[n].spatialNoise[nI].resetFrequency * Time.deltaTime)
                    {
                        closeShapeShift = 1;
                        if (closedShape)
                            closeShapeShift = 0;
                        for (int i = 0; i < segmNums[n] + closeShapeShift; i++)
                        {
                            arcTangentsShift[n, i * 2] = arcTangentsShift[n, i * 2] * RandomXYQuaternion(arcs[n].spatialNoise[nI].scaleMovement * GetShiftCoef((float)i / segmNums[n]));
                            arcTangentsShift[n, i * 2 + 1] = arcTangentsShift[n, i * 2 + 1] * RandomXYQuaternion(arcs[n].spatialNoise[nI].scaleMovement * GetShiftCoef((float)i / segmNums[n]));
                        }
                    }
                    else
                    {
                        ResetArcNoise(n, nI);
                    }
                    break;
            }
        }

        closeShapeShift = 1;
        if (closedShape)
            closeShapeShift = 0;


        if (arcs[n].nesting.Nested && !arcs[n].nesting.combinedNesting)
            for (int i = 0; i < segmNums[n] + closeShapeShift; i++)
                arcPoints[n, i] = GetArcPoint((float)i / segmNums[n] * overlapCeof, arcs[n].nesting.parentArcIndex) + shiftVectors[n, i] * sizeMultiplier;
        else if (arcs[n].nesting.Nested && arcs[n].nesting.combinedNesting)
            for (int i = 0; i < segmNums[n] + closeShapeShift; i++)
                arcPoints[n, i] = Vector3.Lerp(GetArcPoint((float)i / segmNums[n] * overlapCeof, arcs[n].nesting.parentArcIndex),
                                              GetArcPoint(Mathf.Clamp01((float)i / segmNums[n] * overlapCeof - 0.001f), arcs[n].nesting.secondaryArcIndex),
                                              arcs[n].nesting.nestingCoef) + shiftVectors[n, i] * sizeMultiplier;
        else
            for (int i = 0; i < segmNums[n] + closeShapeShift; i++)
                arcPoints[n, i] = CalcShapePoint((float)i / segmNums[n] * overlapCeof) + shiftVectors[n, i] * sizeMultiplier;

        switch (arcs[n].sizeOptions.interpolation)
        {
            case InterpolationType.CatmullRom_Splines:
                if (closedShape)
                {
                    for (int i = 0; i < segmNums[n]; i++)
                    {
                        arcTangents[n, i] = (arcPoints[n, AddCyclicShift(i, 1, segmNums[n] - 1)] - arcPoints[n, AddCyclicShift(i, -1, segmNums[n] - 1)]) / 2;
                    }
                }
                else
                {
                    arcTangents[n, 0] = arcPoints[n, 1] - arcPoints[n, 0];
                    arcTangents[n, segmNums[n]] = arcPoints[n, segmNums[n]] - arcPoints[n, segmNums[n] - 1];
                    for (int i = 1; i < segmNums[n]; i++)
                    {
                        arcTangents[n, i] = (arcPoints[n, i + 1] - arcPoints[n, i - 1]) / 2;
                    }
                }
                break;
        }
    }



    protected Vector3 CalcArcPoint(float point, int n)
    {
        int st = 0;
        int end = 1;
        if (closedShape)
        {
            st = Mathf.FloorToInt(point * segmNums[n]);
            if (point == 1)
                st -= 1;
            if (st == segmNums[n] - 1)
                end = 0;
            else
                end = st + 1;
        }
        else
        {
            st = Mathf.FloorToInt(point * segmNums[n]);
            if (point != 1)
                end = st + 1;
            else
            {
                end = st;
                st -= 1;
            }
        }


        switch (arcs[n].sizeOptions.interpolation)
        {
            case InterpolationType.CatmullRom_Splines:
                return HermiteCurvePoint(point * segmNums[n] - st, arcPoints[n, st], arcTangentsShift[n, st * 2] * arcTangents[n, st], arcPoints[n, end], arcTangentsShift[n, end * 2 + 1] * arcTangents[n, end]);
            //break;
            case InterpolationType.Linear:
                return arcPoints[n, st] + (arcPoints[n, end] - arcPoints[n, st]) * (point * segmNums[n] - st);
            //break;
            default:
                return arcPoints[n, st] + (arcPoints[n, end] - arcPoints[n, st]) * (point * segmNums[n] - st);
                //break;
        }
    }


    public Vector3 CalcShapePoint(float point)
    {
        //point = PointShift (point);
        float pos = point * shapeLength;
        int stTr = 0;
        int endTr = 1;
        float localPos = 0;
        for (int i = 0; i < shapeKeyLocations.Length - 1; i++)
        {
            if (pos > shapeKeyLocations[i] && pos <= shapeKeyLocations[i + 1])
            {
                stTr = i;
                endTr = i + 1;
                localPos = 1 - (shapeKeyLocations[i + 1] - pos) / (shapeKeyLocations[i + 1] - shapeKeyLocations[i]);
                break;
            }
        }

        if (closedShape && endTr == shapeKeyLocations.Length - 1)
        {
            stTr = resultingShape.Length - 1;
            endTr = 0;
        }

        switch (interpolation)
        {
            case InterpolationType.CatmullRom_Splines:
				return HermiteCurvePoint(localPos, resultingShape[stTr].position, shapeTangents[stTr], resultingShape[endTr].position, shapeTangents[endTr]);
            case InterpolationType.Linear:
				return resultingShape[stTr].position + (resultingShape[endTr].position - resultingShape[stTr].position) * localPos;
        }
        return Vector3.zero;
    }

    public Vector3 GetArcPoint(float point, int arcIndex)
    {
        float pos = point * (vertexCount[arcIndex] - 1);
        int ind1 = Mathf.Clamp(Mathf.FloorToInt(pos), 0, vertexCount[arcIndex] - 1);
        int ind2 = Mathf.Clamp(Mathf.CeilToInt(pos), 0, vertexCount[arcIndex] - 1);
        float koef = pos - Mathf.Floor(pos);
        Vector3 vert1;
        Vector3 vert2;
        if (vertices[arcIndex][ind1] == Vector3.zero)
            vert1 = CalcArcPoint(point, arcIndex);
        else
            vert1 = vertices[arcIndex][ind1];
        if (vertices[arcIndex][ind2] == Vector3.zero)
            vert2 = CalcArcPoint(point, arcIndex);
        else
            vert2 = vertices[arcIndex][ind2];
        return vert1 * (1 - koef) + vert2 * koef;
    }

    public Vector3 GetOldArcPoint(float point, int arcIndex)
    {
        float pos = point * (oldVertexCount[arcIndex] - 1);
        int ind1 = Mathf.Clamp(Mathf.FloorToInt(pos), 0, oldVertexCount[arcIndex] - 1);
        int ind2 = Mathf.Clamp(Mathf.CeilToInt(pos), 0, oldVertexCount[arcIndex] - 1);
        float koef = pos - Mathf.Floor(pos);
        Vector3 oldVert1;
        Vector3 oldVert2;
        if (oldVertices[arcIndex][ind1] == Vector3.zero)
            oldVert1 = CalcArcPoint(point, arcIndex);
        else
            oldVert1 = oldVertices[arcIndex][ind1];
        if (oldVertices[arcIndex][ind2] == Vector3.zero)
            oldVert2 = CalcArcPoint(point, arcIndex);
        else
            oldVert2 = oldVertices[arcIndex][ind2];
        return oldVert1 * (1 - koef) + oldVert2 * koef;
    }


    public float GetShiftCoef(float point)
    {
        if (easeInOutOptions.useEaseInOut)
        {
            float length = point * shapeLength;
            if (length > easeInOutOptions.distance / 2 && length < shapeLength - easeInOutOptions.distance / 2)
                return easeInOutOptions.easeInOutCurve.Evaluate(0.5f);
            else
            {
                if (length < easeInOutOptions.distance / 2)
                    return easeInOutOptions.easeInOutCurve.Evaluate(length / easeInOutOptions.distance);
                else
                    return easeInOutOptions.easeInOutCurve.Evaluate(1 - (shapeLength - length) / easeInOutOptions.distance);
            }
        }
        else
            return 1;
    }


    public void ResetArc(int n)
    {
        float point;
        for (int i = 0; i < arcs[n].spatialNoise.Length; i++)
        {
            ResetArcNoise(n, i);
        }   
        if (arcs[n].nesting.Nested && !arcs[n].nesting.combinedNesting)
        {
            for (int i = 0; i < segmNums[n]; i++)
            {
                point = (float)i / segmNums[n];
                arcPoints[n, i] = GetArcPoint(point, arcs[n].nesting.parentArcIndex) + shiftVectors[n, i] * sizeMultiplier;
            }
        }
        else if (arcs[n].nesting.Nested && arcs[n].nesting.combinedNesting)
        {
            for (int i = 0; i < segmNums[n]; i++)
            {
                point = (float)i / segmNums[n];
                arcPoints[n, i] = Vector3.Lerp(GetArcPoint(point, arcs[n].nesting.parentArcIndex),
                                              GetArcPoint(Mathf.Clamp01(point - 0.001f), arcs[n].nesting.secondaryArcIndex),
                                              arcs[n].nesting.nestingCoef) + shiftVectors[n, i] * sizeMultiplier;
            }
        }
        else
        {
            for (int i = 0; i < segmNums[n]; i++)
            {
                point = (float)i / segmNums[n];
                arcPoints[n, i] = CalcShapePoint(point) + shiftVectors[n, i] * sizeMultiplier;
            }
        }

    }

    public void ResetArcNoise(int n, int noiseInd)
    {
        switch (arcs[n].spatialNoise[noiseInd].type)
        {
            case SpatialNoiseType.CubicRandomization:
                for (int i = 0; i <= segmNums[n]; i++)
					shiftVectors[n, i] = RandomVector3(arcs[n].spatialNoise[noiseInd].scale) * GetShiftCoef((float)i / segmNums[n]) * sizeMultiplier;
                break;
            case SpatialNoiseType.TangentRandomization:
                for (int i = 0; i <= segmNums[n]; i++)
                {
                    arcTangentsShift[n, i * 2] = RandomXYQuaternion(arcs[n].spatialNoise[noiseInd].scale * GetShiftCoef((float)i / segmNums[n]));
                    arcTangentsShift[n, i * 2 + 1] = arcTangentsShift[n, i * 2];
                }
                break;
            case SpatialNoiseType.BrokenTangentRandomization:
                for (int i = 0; i <= segmNums[n]; i++)
                {
                    arcTangentsShift[n, i * 2] = RandomXYQuaternion(arcs[n].spatialNoise[noiseInd].scale * GetShiftCoef((float)i / segmNums[n]));
                    arcTangentsShift[n, i * 2 + 1] = RandomXYQuaternion(arcs[n].spatialNoise[noiseInd].scale * GetShiftCoef((float)i / segmNums[n]));
                }
                break;
        }
    }


    protected float GetFlareBrightness(Vector3 currentCameraPosition, Vector3 flarePosition, FlareInfo flInfo, float multiplier = 1)
    {
        float distance = Mathf.Clamp((currentCameraPosition - flarePosition).magnitude, flInfo.maxBrightnessDistance, flInfo.minBrightnessDistance) - flInfo.maxBrightnessDistance;
        return Mathf.Lerp(flInfo.maxBrightness, flInfo.minBrightness, distance / (flInfo.minBrightnessDistance - flInfo.maxBrightnessDistance)) * multiplier;
    }

    protected void SetFlares(int n)
    {
		if (currentCamera == null)
			currentCamera = Camera.main;
        float multiplier = 1;
        if (arcs[n].flaresOptions.startFlare.enabled)
        {
			startFlare.transform.position = resultingShape[0].position;

            if (arcs[n].flaresOptions.useNoiseMask)
                multiplier = arcs[n].flaresOptions.noiseMaskPowerCurve.Evaluate(noiseOffsets[n]);

			startFlare.brightness = GetFlareBrightness(currentCamera.transform.position, resultingShape[0].position, arcs[n].flaresOptions.startFlare,
														GetArcWidthAtPoint(n,0) / maxStartWidth[n]) * multiplier;
			
            startFlare.color = arcs[n].colorOptions.startColor.Evaluate(elapsedTime / lifetime);
        }
        if (arcs[n].flaresOptions.endFlare.enabled)
        {
			endFlare.transform.position = resultingShape[resultingShape.Length - 1].position;

            if (arcs[n].flaresOptions.useNoiseMask)
                multiplier = arcs[n].flaresOptions.noiseMaskPowerCurve.Evaluate(AddCyclicShift(noiseScale[n] - Mathf.Floor(noiseScale[n]), noiseOffsets[n], 1));

            endFlare.brightness = GetFlareBrightness(currentCamera.transform.position, resultingShape[resultingShape.Length - 1].position, arcs[n].flaresOptions.endFlare,
													 GetArcWidthAtPoint(n,1) / maxStartWidth[n]) * multiplier;
			
            if (arcs[n].colorOptions.onlyStartColor)
                endFlare.color = arcs[n].colorOptions.startColor.Evaluate(elapsedTime / lifetime);
            else
                endFlare.color = arcs[n].colorOptions.endColor.Evaluate(elapsedTime / lifetime);
        }
    }
		

	public void Initialize()
    {
        oldShapeLength = shapeLength;

        bool anyLights = false;

        for (int n = 0; n < arcs.Length; n++)
        {
            //Particle emitters initialization
            for (int q = 0; q < arcs[n].emissionOptions.Length; q++)
            {
                if (emitterSystems[n][q] == null && arcs[n].emissionOptions[q].shurikenPrefab != null)
                {
					GameObject partGameObject = (GameObject)GameObject.Instantiate(arcs[n].emissionOptions[q].shurikenPrefab.gameObject);
                    partGameObject.name = "EmitterObject " + gameObject.name + " " + n.ToString() + "," + q.ToString();
                    emitterSystems[n][q] = partGameObject.GetComponent<ParticleSystem>();
                    ParticleSystem.EmissionModule emission = emitterSystems[n][q].emission;
                    if (emission.enabled)
                        emission.enabled = false;
                    if (!arcs[n].emissionOptions[q].emitAfterRayDeath)
                        partGameObject.transform.parent = transform;
                    else
                    {
                        emitterDestructors[n][q] = partGameObject.AddComponent<ArcReactor_EmitterDestructor>();
                        emitterDestructors[n][q].partSystem = emitterSystems[n][q];
                        emitterDestructors[n][q].enabled = false;
                    }
                    partGameObject.transform.position = transform.position;
                    partGameObject.transform.rotation = transform.rotation;
                }
            }


            //Lights initialization
            if (arcs[n].lightsOptions.lights)
            {
                for (int i = 0; i < lightsCount[n]; i++)
                {
                    Destroy(lights[n, i].gameObject);
                }
            }
            anyLights |= arcs[n].lightsOptions.lights;
            lightsCount[n] = Mathf.Max((int)(shapeLength * 2 / arcs[n].lightsOptions.lightsRange + 1), 2);

            //Segment and vertex initialization
            segmNums[n] = Mathf.Max((int)(shapeLength / (arcs[n].sizeOptions.segmentLength * sizeMultiplier)) + arcs[n].sizeOptions.minNumberOfSegments, 2);
            vertexCount[n] = segmNums[n] * (arcs[n].sizeOptions.numberOfSmoothingSegments + 1) + 1;
            oldVertexCount[n] = vertexCount[n];
            oldVertices[n] = new Vector3[vertexCount[n]];
            vertices[n] = new Vector3[vertexCount[n]];
            //lrends[n].SetVertexCount(vertexCount[n]);
			lrends[n].positionCount = vertexCount[n];

            //Flares placing
            if (arcs[n].flaresOptions.startFlare.enabled && startFlare == null)
            {
                GameObject obj = new GameObject(gameObject.name + "_Start_flare");
                obj.transform.parent = transform;
                startFlare = obj.gameObject.AddComponent<LensFlare>();
                startFlare.flare = arcs[n].flaresOptions.startFlare.flare;
                startFlare.fadeSpeed = arcs[n].flaresOptions.startFlare.fadeSpeed;			
            }
            if (arcs[n].flaresOptions.endFlare.enabled && endFlare == null)
            {
                GameObject obj = new GameObject(gameObject.name + "_End_flare");
                obj.transform.parent = transform;
                endFlare = obj.gameObject.AddComponent<LensFlare>();
                endFlare.flare = arcs[n].flaresOptions.endFlare.flare;
                endFlare.fadeSpeed = arcs[n].flaresOptions.endFlare.fadeSpeed;
            }

        }
			        
        arcPoints = new Vector3[arcs.Length, segmNums.Max() + 2];
        shiftVectors = new Vector3[arcs.Length, segmNums.Max() + 2];
        arcTangents = new Vector3[arcs.Length, segmNums.Max() + 2];
        arcTangentsShift = new Quaternion[arcs.Length, segmNums.Max() * 2 + 2];

        for (int n = 0; n < arcs.Length; n++)
        {
            ResetArc(n);
        }

        if (anyLights)
        {
            GameObject lightObject;
            lights = new Light[arcs.Length, lightsCount.Max()];
            lightsTransforms = new Transform[arcs.Length, lightsCount.Max() + 1];
            for (int n = 0; n < arcs.Length; n++)
            {
                if (arcs[n].lightsOptions.lights)
                {
                    for (int i = 0; i < lightsCount[n]; i++)
                    {
                        lightObject = new GameObject("ArcLight");
                        lightObject.transform.parent = transform;
                        lightsTransforms[n, i] = lightObject.transform;
                        lights[n, i] = lightObject.AddComponent<Light>();
                        lights[n, i].type = LightType.Point;
                        lights[n, i].renderMode = arcs[n].lightsOptions.renderMode;
                        lights[n, i].range = arcs[n].lightsOptions.lightsRange;
                    }
                }
            }
        }
    }

    protected void SetShapeArrays()
    {
        int shapeLen = Mathf.Max(shapeTransforms.Length, shapePoints.Length);
        oldShapeTransformsSize = shapeLen;
        if (closedShape)
        {
            shapeKeyLocations = new float[shapeLen + 1];
            shapeKeyNormalizedLocations = new float[shapeLen + 1];
        }
        else
        {
            shapeKeyLocations = new float[shapeLen];
            shapeKeyNormalizedLocations = new float[shapeLen];
        }
        shapeTangents = new Vector3[shapeLen];
    }


    void Start()
    {
               
        //Service array initialization, actual data creation happens at Initialize()
        emitterSystems = new ParticleSystem[arcs.Length][];
		particleBuffers = new ParticleSystem.Particle[arcs.Length][][];
        emitterDestructors = new ArcReactor_EmitterDestructor[arcs.Length][];
        for (int n = 0; n < arcs.Length; n++)
        {
            emitterSystems[n] = new ParticleSystem[arcs[n].emissionOptions.Length];
            emitterDestructors[n] = new ArcReactor_EmitterDestructor[arcs[n].emissionOptions.Length];
			particleBuffers[n] = new ParticleSystem.Particle[arcs[n].emissionOptions.Length][];
			for (int i = 0; i < arcs[n].emissionOptions.Length; i++)
				particleBuffers[n][i] = new ParticleSystem.Particle[arcs[n].emissionOptions[i].shurikenPrefab.main.maxParticles];
        }
        lrends = new LineRenderer[arcs.Length];
        segmNums = new int[arcs.Length];
        lightsCount = new int[arcs.Length];
        vertexCount = new int[arcs.Length];
        oldVertexCount = new int[arcs.Length];
        noiseOffsets = new float[arcs.Length];
        noiseScale = new float[arcs.Length];
        maxStartWidth = new float[arcs.Length];
        maxEndWidth = new float[arcs.Length];
        coreCoefs = new float[arcs.Length];
        vertices = new Vector3[arcs.Length][];
        oldVertices = new Vector3[arcs.Length][];


        //Init
        SetShapeArrays();

        GameObject rayLineRenderer;
        for (int n = 0; n < arcs.Length; n++)
        {
            rayLineRenderer = new GameObject("ArcLineRenderer");
            rayLineRenderer.transform.parent = transform;
			rayLineRenderer.layer = linerendererLayer.LayerIndex;
            lrends[n] = rayLineRenderer.AddComponent<LineRenderer>();
			if (arcs[n].sizeOptions.shapeType == ShapeTypes.start_curve_as_shape)
			{
				if (sizeMultiplier != 1)
				{
					AnimationCurve curve = new AnimationCurve();
					curve.keys = arcs[n].sizeOptions.startWidthCurve.keys;
					for (int i = 0; i < curve.keys.Length; i++)
						curve.keys[i].value *= sizeMultiplier;
					lrends[n].widthCurve = curve;
				}
				else
					lrends[n].widthCurve = arcs[n].sizeOptions.startWidthCurve;
			}
			lrends[n].numCornerVertices = arcs[n].sizeOptions.cornerVertices;
			lrends[n].numCapVertices = arcs[n].sizeOptions.capVectices;
            lrends[n].material = arcs[n].material;
            lrends[n].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lrends[n].receiveShadows = false;
            if (customSorting)
            {
                lrends[n].sortingLayerName = sortingLayerName;
                lrends[n].sortingOrder = sortingOrder;
            }

            //texture setup
            if (arcs[n].textureOptions.shapeTexture != null)
                lrends[n].material.SetTexture("_MainTex", arcs[n].textureOptions.shapeTexture);
            if (arcs[n].textureOptions.noiseTexture != null)
                lrends[n].material.SetTexture("_NoiseMask", arcs[n].textureOptions.noiseTexture);

            //Calculating maximum widths
            float maxWidth = 0;
            if (arcs[n].flaresOptions.startFlare.enabled)
            {
                for (int i = 0; i <= maxCalcDetalization; i++)
                {
                    if (maxWidth < arcs[n].sizeOptions.startWidthCurve.Evaluate((float)i / maxCalcDetalization))
                        maxWidth = arcs[n].sizeOptions.startWidthCurve.Evaluate((float)i / maxCalcDetalization);
                }
                maxStartWidth[n] = maxWidth;
            }

            if (arcs[n].flaresOptions.endFlare.enabled)
            {
				if (arcs[n].sizeOptions.shapeType != ShapeTypes.start_end)
                {
                    if (arcs[n].flaresOptions.startFlare.enabled)
                        maxEndWidth[n] = maxStartWidth[n];
                    else
                    {
                        for (int i = 0; i <= maxCalcDetalization; i++)
                        {
                            if (maxWidth < arcs[n].sizeOptions.startWidthCurve.Evaluate((float)i / maxCalcDetalization))
                                maxWidth = arcs[n].sizeOptions.startWidthCurve.Evaluate((float)i / maxCalcDetalization);
                        }
                        maxStartWidth[n] = maxWidth;
                        maxEndWidth[n] = maxStartWidth[n];
                    }
                }
                else
                {
                    maxWidth = 0;
                    for (int i = 0; i <= maxCalcDetalization; i++)
                        if (maxWidth < arcs[n].sizeOptions.endWidthCurve.Evaluate((float)i / maxCalcDetalization))
                            maxWidth = arcs[n].sizeOptions.endWidthCurve.Evaluate((float)i / maxCalcDetalization);
                    maxEndWidth[n] = maxWidth;
                }
            }
        }

        CalculateShape();

        //Adding this system to performance manager if it exists
        if (ArcReactor_Manager.Instance != null)
            ArcReactor_Manager.Instance.AddArcSystem(this);
    }


    public Vector3 RandomVector3(float range)
    {
        return new Vector3(UnityEngine.Random.Range(-range, range),
                           UnityEngine.Random.Range(-range, range),
                           UnityEngine.Random.Range(-range, range));
    }


	public void DestroyArc()
	{
		for (int i = 0; i < Mathf.Min(shapeTransforms.Length, transformsDestructionFlags.Length); i++)
		{
			if (transformsDestructionFlags[i])
			{
				Destroy(shapeTransforms[i].gameObject);
			}
		}
		for (int n = 0; n < arcs.Length; n++)
		{
			for (int i = 0; i < arcs[n].emissionOptions.Length; i++)
			{
				if (arcs[n].emissionOptions[i].emitAfterRayDeath)
				{
					emitterDestructors[n][i].onlyDisable = false;
					emitterDestructors[n][i].enabled = true;
				}
			}
		}
		if (playbackMessages)
			messageReciever.SendMessage("ArcReactorPlayback", this);
		Destroy(gameObject);
	}

	public void DisableArc()
	{
		for (int i = 0; i < Mathf.Min(shapeTransforms.Length, transformsDestructionFlags.Length); i++)
		{
			if (transformsDestructionFlags[i])
			{
				Destroy(shapeTransforms[i].gameObject);
			}
		}
		for (int n = 0; n < arcs.Length; n++)
		{
			for (int i = 0; i < arcs[n].emissionOptions.Length; i++)
			{
				if (arcs[n].emissionOptions[i].emitAfterRayDeath)
				{
					emitterDestructors[n][i].onlyDisable = true;
					emitterDestructors[n][i].enabled = true;
				}
			}
		}
		if (playbackMessages)
			messageReciever.SendMessage("ArcReactorPlayback", this);
		gameObject.SetActive(false);
	}

	public void EnableArc()
	{
		for (int n = 0; n < arcs.Length; n++)
		{
			for (int i = 0; i < arcs[n].emissionOptions.Length; i++)
			{
				emitterSystems[n][i].gameObject.SetActive(true);
				if (emitterDestructors[n][i] != null)
					emitterDestructors[n][i].enabled = false;
			}
		}
		gameObject.SetActive(true);
	}

    void Update()
    {
        //Phase shifting
        for (int n = 0; n < arcs.Length; n++)
        {
            foreach (OscillationInfo osc in arcs[n].oscillations)
            {
                osc.phase += osc.phaseMovementSpeed * Time.deltaTime;
                if (osc.phase > 360)
                    osc.phase = osc.phase - 360;
                if (osc.phase < 0)
                    osc.phase = osc.phase + 360;
            }
        }

        //Time management
        if (!freeze)
        {	
			if (!playBackward)
			{
	        	elapsedTime += Time.deltaTime;
			}
	        else
			{
                elapsedTime -= Time.deltaTime;
			}
        }		

        if (elapsedTime > lifetime)
        {			
            switch (playbackType)
            {
                case ArcsPlaybackType.once:
					if (playbackMessages)
						messageReciever.SendMessage("ArcReactorPlayback", this);
					if (ArcReactor_PoolManager.Instance != null)
						ArcReactor_PoolManager.Instance.SetEntityAsFree(this);
					else
						DestroyArc();
                break;
                case ArcsPlaybackType.loop:
                    elapsedTime -= lifetime;
                    if (playbackMessages)
                        messageReciever.SendMessage("ArcReactorPlayback", this);
                break;
                case ArcsPlaybackType.pingpong:
                    playBackward = true;
                    elapsedTime = lifetime;
                    if (playbackMessages)
                        messageReciever.SendMessage("ArcReactorPlayback", this);
                break;
                case ArcsPlaybackType.clamp:
                    elapsedTime = lifetime;
                    freeze = true;
                    if (playbackMessages)
                        messageReciever.SendMessage("ArcReactorPlayback", this);
                break;
				case ArcsPlaybackType.pingpong_once:
					playBackward = true;
					elapsedTime = lifetime;
					if (playbackMessages)
						messageReciever.SendMessage("ArcReactorPlayback", this);
				break;
				case ArcsPlaybackType.pingpong_clamp_once:
					elapsedTime = lifetime;
					freeze = true;
					playBackward = true;
					if (playbackMessages)
						messageReciever.SendMessage("ArcReactorPlayback", this);
				break;
            }

        }
        if (elapsedTime < 0)
        {			
            playBackward = false;
            elapsedTime = 0;
			if (playbackType == ArcsPlaybackType.pingpong_clamp_once || playbackType == ArcsPlaybackType.pingpong_once)
			{
				if (playbackMessages)
					messageReciever.SendMessage("ArcReactorPlayback", this);
				if (ArcReactor_PoolManager.Instance != null)
					ArcReactor_PoolManager.Instance.SetEntityAsFree(this);
				else
					DestroyArc();				
			}
        }

    }


    public Vector3 GetArcEndPosition(int arcIndex)
    {
        return GetArcPoint(GetArcEndPoint(arcIndex), arcIndex);
    }



    public float GetArcEndPoint(int arcIndex)
    {
        switch (arcs[arcIndex].propagationOptions.propagationType)
        {
            case PropagationType.globalSpaceSpeed:
                return Mathf.Min(vertexCount[arcIndex] * arcs[arcIndex].propagationOptions.globalSpeed * elapsedTime / shapeLength, vertexCount[arcIndex]) / vertexCount[arcIndex];
            case PropagationType.localTimeCurve:
                return Mathf.Clamp01(arcs[arcIndex].propagationOptions.timeCurve.Evaluate(elapsedTime / lifetime));
            case PropagationType.instant:
                return 1;
            default:
                return 1;
        }
    }

	public void ExcludeFromPool()
	{
		if (ArcReactor_PoolManager.Instance != null)
		{			
			ArcReactor_PoolManager.Instance.activeEntities.Remove(this);
		}
	}


    void LateUpdate()
    {
        float lifetimePos = elapsedTime / lifetime;
        CalculateShape();
        if (localSpaceOcillations)
            oscNormal = transform.rotation * oscillationNormal;
        else
            oscNormal = oscillationNormal;
        for (int n = 0; n < arcs.Length; n++)
        {
            vertices[n].CopyTo(oldVertices[n], 0);

            Color StartColor = arcs[n].colorOptions.startColor.Evaluate(lifetimePos);
            Color EndColor;
            if (arcs[n].colorOptions.onlyStartColor)
                EndColor = StartColor;
            else
                EndColor = arcs[n].colorOptions.endColor.Evaluate(lifetimePos);
            Color coreColor = arcs[n].colorOptions.coreColor.Evaluate(lifetimePos);

            lrends[n].material.SetColor("_StartColor", StartColor);
            lrends[n].material.SetColor("_EndColor", EndColor);

            lrends[n].material.SetColor("_CoreColor", coreColor);

            if (arcs[n].colorOptions.coreJitter > 0)
            {
                coreCoefs[n] = arcs[n].colorOptions.coreCurve.Evaluate(lifetimePos) + UnityEngine.Random.Range(-arcs[n].colorOptions.coreJitter * 0.5f, arcs[n].colorOptions.coreJitter * 0.5f);
                lrends[n].material.SetFloat("_CoreCoef", coreCoefs[n]);
            }
            else
            {
                coreCoefs[n] = arcs[n].colorOptions.coreCurve.Evaluate(lifetimePos);
                lrends[n].material.SetFloat("_CoreCoef", coreCoefs[n]);
            }

            //Fading
            switch (arcs[n].colorOptions.fade)
            {
                case FadeTypes.none:
                    lrends[n].material.SetFloat("_FadeLevel", 0.001f);
                    break;
                case FadeTypes.relativePoint:
                    lrends[n].material.SetFloat("_FadeLevel", Mathf.Max(arcs[n].colorOptions.fadePoint, 0.001f));
                    break;
                case FadeTypes.worldspacePoint:
                    lrends[n].material.SetFloat("_FadeLevel", Mathf.Max(Mathf.Clamp01(arcs[n].colorOptions.fadePoint / shapeLength), 0.001f));
                    break;
            }
            switch (arcs[n].colorOptions.frontFade)
            {
                case FadeTypes.none:
                    lrends[n].material.SetFloat("_FrontFadeLevel", 0.001f);
                    break;
                case FadeTypes.relativePoint:
                    lrends[n].material.SetFloat("_FrontFadeLevel", Mathf.Max(arcs[n].colorOptions.frontFadePoint, 0.001f));
                    break;
                case FadeTypes.worldspacePoint:
                    lrends[n].material.SetFloat("_FrontFadeLevel", Mathf.Max(Mathf.Clamp01(arcs[n].colorOptions.frontFadePoint / shapeLength), 0.001f));
                    break;
            }

            //Ray size change            
			if (arcs[n].sizeOptions.shapeType != ShapeTypes.start_curve_as_shape)
			{
				lrends[n].startWidth = GetArcWidthAtPoint(n,0);
				lrends[n].endWidth = GetArcWidthAtPoint(n,1);
			}            

            float vertexCnt = vertexCount[n];
            switch (arcs[n].propagationOptions.propagationType)
            {
                case PropagationType.globalSpaceSpeed:
                    vertexCnt = Mathf.Min(vertexCount[n] * arcs[n].propagationOptions.globalSpeed * elapsedTime / shapeLength, vertexCount[n]);
					lrends[n].positionCount = Mathf.CeilToInt(vertexCnt);
                    break;
                case PropagationType.localTimeCurve:
                    vertexCnt = Mathf.Min(vertexCount[n] * arcs[n].propagationOptions.timeCurve.Evaluate(lifetimePos), vertexCount[n]);                    
					lrends[n].positionCount = Mathf.Max(Mathf.CeilToInt(vertexCnt), 0);
                    break;
            }

            //Texture handling
            if (arcs[n].textureOptions.noiseTexture != null)
            {
                lrends[n].material.SetFloat("_NoiseCoef", arcs[n].textureOptions.noiseCoef.Evaluate(lifetimePos));
                if (arcs[n].textureOptions.animateTexture)
                {
                    noiseOffsets[n] += arcs[n].textureOptions.noiseSpeed * Time.deltaTime;
                    if (noiseOffsets[n] > 1)
                        noiseOffsets[n] -= 1;
                    if (noiseOffsets[n] < 0)
                        noiseOffsets[n] += 1;
                    noiseScale[n] = vertexCnt / vertexCount[n] * shapeLength / arcs[n].textureOptions.tileSize;
                    lrends[n].material.SetTextureScale("_NoiseMask", new Vector2(noiseScale[n], 1));
                    lrends[n].material.SetTextureOffset("_NoiseMask", new Vector2(noiseOffsets[n], 1));
                }
                else
                {
                    noiseScale[n] = vertexCnt / vertexCount[n] * shapeLength / arcs[n].textureOptions.tileSize;
                    lrends[n].material.SetTextureScale("_NoiseMask", new Vector2(noiseScale[n], 1));
                }
            }

            SetFlares(n);
            SetArcShape(n);
            Vector3 curVertexPos;
            curVertexPos = CalcArcPoint(0, n);
            Vector3 nextVertexPos = Vector3.zero;
            Vector3 direction = Vector3.zero;
            float pos;
            int currentShapeKey = 1;
            for (int curVertex = 0; curVertex < vertexCnt - 1; curVertex++)
            {
                pos = (float)curVertex / vertexCount[n];

                if (arcs[n].sizeOptions.snapSegmentsToShape && Mathf.Abs(shapeKeyNormalizedLocations[currentShapeKey] - pos) * vertexCount[n] < 0.5)
                {
                    pos = shapeKeyNormalizedLocations[currentShapeKey];
                    curVertexPos = shapeTransforms[currentShapeKey].position;
                    currentShapeKey++;
                }
                nextVertexPos = CalcArcPoint((float)(curVertex + 1) / vertexCount[n], n);
                direction = nextVertexPos - curVertexPos;
                vertices[n][curVertex] = curVertexPos
                                        + CalculateOscillationShift(direction, pos * shapeLength, n) * GetShiftCoef(pos)
                                        + CalculateCurveShift(direction, pos * ShapeLength, n);
                lrends[n].SetPosition(curVertex, vertices[n][curVertex]);
                curVertexPos = nextVertexPos;
            }
            if (Mathf.CeilToInt(vertexCnt) > 0 && Mathf.CeilToInt(vertexCnt) <= vertexCount[n])
            {
                vertices[n][Mathf.CeilToInt(vertexCnt) - 1] = CalculateOscillationShift(direction, shapeLength * (vertexCnt) / vertexCount[n], n) * GetShiftCoef(vertexCnt / vertexCount[n])
                                                               + CalcArcPoint(vertexCnt / vertexCount[n], n);
                lrends[n].SetPosition(Mathf.CeilToInt(vertexCnt) - 1, vertices[n][Mathf.CeilToInt(vertexCnt) - 1]);
            }




            //Particles emissions
            for (int i = 0; i < arcs[n].emissionOptions.Length; i++)
            {
                if (arcs[n].emissionOptions[i].emit)
                {                    
                    int particleCount = (int)(UnityEngine.Random.value + vertexCnt / vertexCount[n] * shapeLength * arcs[n].emissionOptions[i].particlesPerMeter * Time.deltaTime * arcs[n].emissionOptions[i].emissionDuringLifetime.Evaluate(lifetimePos));
                    float arcEndPoint = vertexCnt / vertexCount[n];
                    float radiusCoef = arcs[n].emissionOptions[i].radiusCoefDuringLifetime.Evaluate(lifetimePos);
                    float directionCoef = arcs[n].emissionOptions[i].directionDuringLifetime.Evaluate(lifetimePos);
                    float radius;
                    float rand = 0;
                    Vector3 randomVect = Vector3.one;
                    Vector3 spaceShiftVect;
					if (emitterSystems[n][i].main.simulationSpace == ParticleSystemSimulationSpace.Local)
                        spaceShiftVect = -emitterSystems[n][i].transform.position;
                    else
                        spaceShiftVect = Vector3.zero;
                    Color emitStartColor;
                    Color emitEndColor;
                    Vector3 emitPos;
                    Vector3 emitDir;
                    emitStartColor = StartColor;
                    emitEndColor = EndColor;
                                       
					int oldParticlesAlive = emitterSystems[n][i].GetParticles(particleBuffers[n][i]);
					particleCount = Mathf.Clamp(particleCount,0,emitterSystems[n][i].main.maxParticles - oldParticlesAlive);
					emitterSystems[n][i].Emit(particleCount);
					emitterSystems[n][i].GetParticles(particleBuffers[n][i]);
					                    
                    for (int q = 0; q < particleCount; q++)
                    {
                        rand = 0.001f + UnityEngine.Random.value * (arcEndPoint - 0.002f); //get random point on arc shape without touching exact end of arc
                        randomVect = UnityEngine.Random.rotation * Vector3.forward;
						radius = GetArcWidthAtPoint(n,rand) * radiusCoef;                        
                        emitPos = GetArcPoint(rand, n);
                        emitDir = (GetArcPoint(rand + 0.001f, n) - emitPos).normalized;

						particleBuffers[n][i][oldParticlesAlive + q].position = Vector3.Lerp(emitPos, GetOldArcPoint(rand, n), UnityEngine.Random.value) + randomVect * radius * sizeMultiplier + spaceShiftVect;
						particleBuffers[n][i][oldParticlesAlive + q].startSize *= sizeMultiplier;
						particleBuffers[n][i][oldParticlesAlive + q].startColor = Color.Lerp(particleBuffers[n][i][oldParticlesAlive + q].startColor,Color.Lerp(emitStartColor, emitEndColor, rand),arcs[n].emissionOptions[i].arcColorInfluence);
						particleBuffers[n][i][oldParticlesAlive + q].velocity = (randomVect * (1f - Mathf.Clamp01(Mathf.Abs(directionCoef))) + emitDir * directionCoef) * particleBuffers[n][i][oldParticlesAlive + q].velocity.magnitude;
                    }
					emitterSystems[n][i].SetParticles(particleBuffers[n][i],oldParticlesAlive + particleCount);
                }
            }


            //Lights placing
            if (arcs[n].lightsOptions.lights && arcs[n].lightsOptions.priority <= performancePriority)
            {
                for (int i = 0; i < lightsCount[n]; i++)
                {
                    if ((float)(i) / lightsCount[n] <= vertexCnt / vertexCount[n])
                    {
                        lights[n, i].enabled = true;
                        Color mainLightColor;
                        if (!arcs[n].colorOptions.onlyStartColor)
                            mainLightColor = Color.Lerp(StartColor, EndColor, (float)(i) / (lightsCount[n] - 1));
                        else
                            mainLightColor = StartColor;
                        lights[n, i].color = Color.Lerp(mainLightColor, coreColor, coreCoefs[n] / 2);                        
						lights[n, i].intensity = arcs[n].lightsOptions.lightsIntensityMultiplyer * GetArcWidthAtPoint(n,(float)i / (segmNums[n] + 1));                        
                        lightsTransforms[n, i].position = GetArcPoint((float)(i) / (lightsCount[n] - 1), n);
                    }
                    else
                    {
                        lights[n, i].enabled = false;
                    }
                }
            }
        }
    }
}
